using System;
using System.Collections.Generic;
using DedicatedServerMod.Utils;
using UnityEngine;
#if IL2CPP
using Il2CppSteamworks;
#else
using Steamworks;
#endif

namespace DedicatedServerMod.API.Client
{
    /// <summary>
    /// Client-side helper for retrieving Steam avatar textures for connected players.
    /// </summary>
    /// <remarks>
    /// This service is available only in client builds and is exposed through
    /// <see cref="S1DS.Client"/>. Avatar textures are loaded from the local Steam client,
    /// cached by SteamID64, and returned as Unity <see cref="Texture2D"/> instances.
    /// <para>
    /// <see cref="GetSteamAvatar(string)"/> returns a cached avatar immediately when available.
    /// If the avatar has not been cached yet, it starts a Steam lookup and returns
    /// <see langword="null"/> until Steam provides the image data.
    /// </para>
    /// <para>
    /// <see cref="RequestSteamAvatar(string, Action{Texture2D})"/> is the preferred API when a
    /// mod wants a callback once the avatar is available.
    /// </para>
    /// </remarks>
    public sealed class ClientSteamAvatarService
    {
        private readonly Dictionary<ulong, Texture2D> _avatarCache = new Dictionary<ulong, Texture2D>();
        private readonly Dictionary<ulong, List<Action<Texture2D>>> _pendingCallbacks = new Dictionary<ulong, List<Action<Texture2D>>>();

        private Callback<AvatarImageLoaded_t> _avatarImageLoadedCallback;
        private bool _isInitialized;

        internal static ClientSteamAvatarService Instance { get; } = new ClientSteamAvatarService();

        private ClientSteamAvatarService()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the Steam avatar helper is initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets a value indicating whether Steam is currently available for avatar lookups.
        /// </summary>
        public bool IsSteamAvailable
        {
            get
            {
                try
                {
                    return SteamAPI.IsSteamRunning();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets a cached Steam avatar texture for the specified SteamID64 when available.
        /// </summary>
        /// <param name="steamId">SteamID64 of the target player.</param>
        /// <returns>
        /// The cached or newly loaded avatar texture, or <see langword="null"/> when the avatar
        /// is still loading or cannot be resolved.
        /// </returns>
        /// <exception cref="ArgumentException"><paramref name="steamId"/> is null, empty, or not a valid SteamID64.</exception>
        public Texture2D GetSteamAvatar(string steamId)
        {
            ulong steamIdValue = ParseSteamId(steamId);
            EnsureInitialized();

            if (TryGetAvatarTexture(steamIdValue, out Texture2D texture, out _))
            {
                return texture;
            }

            return null;
        }

        /// <summary>
        /// Requests a Steam avatar texture and invokes a callback once it becomes available.
        /// </summary>
        /// <param name="steamId">SteamID64 of the target player.</param>
        /// <param name="onLoaded">
        /// Callback invoked with the avatar texture once available, or <see langword="null"/> if the
        /// avatar could not be resolved.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="steamId"/> is null, empty, or not a valid SteamID64.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="onLoaded"/> is null.</exception>
        public void RequestSteamAvatar(string steamId, Action<Texture2D> onLoaded)
        {
            if (onLoaded == null)
            {
                throw new ArgumentNullException(nameof(onLoaded));
            }

            ulong steamIdValue = ParseSteamId(steamId);
            EnsureInitialized();

            if (TryGetAvatarTexture(steamIdValue, out Texture2D texture, out bool isPending))
            {
                onLoaded(texture);
                return;
            }

            if (!IsSteamAvailable)
            {
                onLoaded(null);
                return;
            }

            if (!isPending)
            {
                onLoaded(null);
                return;
            }

            if (!_pendingCallbacks.TryGetValue(steamIdValue, out List<Action<Texture2D>> callbacks))
            {
                callbacks = new List<Action<Texture2D>>();
                _pendingCallbacks.Add(steamIdValue, callbacks);
            }

            callbacks.Add(onLoaded);
        }

        /// <summary>
        /// Removes a cached Steam avatar texture for the specified SteamID64.
        /// </summary>
        /// <param name="steamId">SteamID64 of the cached avatar to remove.</param>
        /// <returns><see langword="true"/> when an avatar was removed; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="steamId"/> is null, empty, or not a valid SteamID64.</exception>
        public bool RemoveCachedSteamAvatar(string steamId)
        {
            ulong steamIdValue = ParseSteamId(steamId);
            if (!_avatarCache.TryGetValue(steamIdValue, out Texture2D texture))
            {
                return false;
            }

            _avatarCache.Remove(steamIdValue);
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }

            return true;
        }

        internal void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            if (IsSteamAvailable)
            {
                _avatarImageLoadedCallback = Callback<AvatarImageLoaded_t>.Create(CreateAvatarImageLoadedDelegate());
            }

            _isInitialized = true;
        }

        internal void Tick()
        {
            if (!_isInitialized || _pendingCallbacks.Count == 0 || !IsSteamAvailable)
            {
                return;
            }

            try
            {
                SteamAPI.RunCallbacks();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Steam avatar callback pump failed: {ex.Message}");
            }
        }

        internal void Shutdown()
        {
            try
            {
                _avatarImageLoadedCallback?.Dispose();
            }
            catch (Exception ex)
            {
                DebugLog.Warning($"Steam avatar callback disposal failed: {ex.Message}");
            }
            finally
            {
                _avatarImageLoadedCallback = null;
            }

            foreach (Texture2D texture in _avatarCache.Values)
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }

            _avatarCache.Clear();
            _pendingCallbacks.Clear();
            _isInitialized = false;
        }

        private static ulong ParseSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                throw new ArgumentException("SteamID64 must be provided.", nameof(steamId));
            }

            if (!ulong.TryParse(steamId, out ulong steamIdValue) || steamIdValue == 0)
            {
                throw new ArgumentException("SteamID64 must be a non-zero unsigned integer.", nameof(steamId));
            }

            return steamIdValue;
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        private void OnAvatarImageLoaded(AvatarImageLoaded_t callback)
        {
            ulong steamIdValue = callback.m_steamID.m_SteamID;
            Texture2D texture = CreateTextureFromImageHandle(callback.m_iImage, steamIdValue);
            if (texture != null)
            {
                if (_avatarCache.TryGetValue(steamIdValue, out Texture2D existingTexture) && existingTexture != null && existingTexture != texture)
                {
                    UnityEngine.Object.Destroy(existingTexture);
                }

                _avatarCache[steamIdValue] = texture;
            }

            CompletePendingCallbacks(steamIdValue, texture);
        }

        private void CompletePendingCallbacks(ulong steamIdValue, Texture2D texture)
        {
            if (!_pendingCallbacks.TryGetValue(steamIdValue, out List<Action<Texture2D>> callbacks))
            {
                return;
            }

            _pendingCallbacks.Remove(steamIdValue);
            for (int i = 0; i < callbacks.Count; i++)
            {
                try
                {
                    callbacks[i]?.Invoke(texture);
                }
                catch (Exception ex)
                {
                    DebugLog.Warning($"Steam avatar callback threw for {steamIdValue}: {ex.Message}");
                }
            }
        }

        private bool TryGetAvatarTexture(ulong steamIdValue, out Texture2D texture, out bool isPending)
        {
            texture = null;
            isPending = false;

            if (_avatarCache.TryGetValue(steamIdValue, out Texture2D cachedTexture) && cachedTexture != null)
            {
                texture = cachedTexture;
                return true;
            }

            if (!IsSteamAvailable)
            {
                DebugLog.Warning("Steam avatar lookup requested while Steam is unavailable.");
                return false;
            }

            int imageHandle = SteamFriends.GetLargeFriendAvatar(new CSteamID(steamIdValue));
            if (imageHandle > 0)
            {
                texture = CreateTextureFromImageHandle(imageHandle, steamIdValue);
                if (texture != null)
                {
                    _avatarCache[steamIdValue] = texture;
                    return true;
                }

                return false;
            }

            if (imageHandle == -1)
            {
                isPending = true;
                SteamFriends.RequestUserInformation(new CSteamID(steamIdValue), false);
            }

            return false;
        }

        private static Texture2D CreateTextureFromImageHandle(int imageHandle, ulong steamIdValue)
        {
            if (imageHandle <= 0)
            {
                return null;
            }

            if (!SteamUtils.GetImageSize(imageHandle, out uint width, out uint height) || width == 0 || height == 0)
            {
                return null;
            }

            byte[] imageData = new byte[width * height * 4];
            if (!SteamUtils.GetImageRGBA(imageHandle, imageData, imageData.Length))
            {
                return null;
            }

            Texture2D texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, mipChain: false);
            texture.name = $"SteamAvatar_{steamIdValue}";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.LoadRawTextureData(imageData);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            UnityEngine.Object.DontDestroyOnLoad(texture);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return texture;
        }

        private Callback<AvatarImageLoaded_t>.DispatchDelegate CreateAvatarImageLoadedDelegate()
        {
#if IL2CPP
            return (Callback<AvatarImageLoaded_t>.DispatchDelegate)new Action<AvatarImageLoaded_t>(OnAvatarImageLoaded);
#else
            return new Callback<AvatarImageLoaded_t>.DispatchDelegate(OnAvatarImageLoaded);
#endif
        }
    }
}
