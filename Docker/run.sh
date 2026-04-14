#!/bin/bash

# Function to find an available display
find_available_display() {
    for i in {100..199}; do
        if ! [ -f "/tmp/.X${i}-lock" ]; then
            echo $i
            return
        fi
    done
    echo "99" # fallback
}

# Clean up any existing display locks and start virtual display
DISPLAY_NUM=$(find_available_display)
export DISPLAY=:${DISPLAY_NUM}

echo "Starting Xvfb on display :${DISPLAY_NUM}"
rm -f /tmp/.X${DISPLAY_NUM}-lock 2>/dev/null || true

Xvfb :${DISPLAY_NUM} -screen 0 1024x768x24 -ac +extension GLX +render -noreset &
XVFB_PID=$!

# Give Xvfb time to start
sleep 5

# Set default values for missing environment variables
S1DS_RUNTIME=$(printf '%s' "${S1DS_RUNTIME:-mono}" | tr '[:upper:]' '[:lower:]')
STEAM_GUARD=${STEAM_GUARD:-""}
STEAMWORKS_REDIST_DIR=${STEAMWORKS_REDIST_DIR:-"/home/steam/steamworks_redist"}
FORCE_STEAMCMD_UPDATE=${FORCE_STEAMCMD_UPDATE:-"false"}
GAME_EXE_PATH="${STEAMAPPDIR}/Schedule I.exe"

case "${S1DS_RUNTIME}" in
    mono)
        DEFAULT_STEAM_BRANCH="alternate"
        MOD_DLL_NAME="DedicatedServerMod_Mono_Server.dll"
        ;;
    il2cpp)
        DEFAULT_STEAM_BRANCH=""
        MOD_DLL_NAME="DedicatedServerMod_Il2cpp_Server.dll"
        ;;
    *)
        echo "ERROR: Unsupported S1DS_RUNTIME '${S1DS_RUNTIME}'. Expected 'mono' or 'il2cpp'."
        kill $XVFB_PID 2>/dev/null || true
        exit 1
        ;;
esac

if [ -z "${STEAM_BRANCH}" ]; then
    STEAM_BRANCH="${DEFAULT_STEAM_BRANCH}"
fi

should_run_steamcmd_update() {
    if [ "${FORCE_STEAMCMD_UPDATE}" = "true" ]; then
        return 0
    fi

    if [ ! -f "${GAME_EXE_PATH}" ]; then
        return 0
    fi

    return 1
}

should_refresh_steamworks_redist() {
    if [ "${FORCE_STEAMCMD_UPDATE}" = "true" ]; then
        return 0
    fi

    for required_dll in steamclient64.dll tier0_s64.dll vstdlib_s64.dll; do
        if [ ! -f "${STEAMWORKS_REDIST_DIR}/${required_dll}" ] \
            && [ ! -f "${STEAMWORKS_REDIST_DIR}/redistributable_bin/${required_dll}" ] \
            && [ ! -f "${STEAMWORKS_REDIST_DIR}/redistributable_bin/win64/${required_dll}" ]; then
            return 0
        fi
    done

    return 1
}

# Update/install the game via SteamCMD only when needed.
echo "SteamCMD update mode: FORCE_STEAMCMD_UPDATE=${FORCE_STEAMCMD_UPDATE}"
echo "DedicatedServerMod runtime: ${S1DS_RUNTIME}"
echo "DedicatedServerMod bootstrap DLL: ${MOD_DLL_NAME}"
echo "App ID: ${STEAMAPPID}"
echo "Install directory: ${STEAMAPPDIR}"
if [ -n "${STEAM_BRANCH}" ]; then
    echo "Steam branch: ${STEAM_BRANCH}"
else
    echo "Steam branch: default"
fi

cd /home/steam/steamcmd

if should_run_steamcmd_update; then
    echo "Starting SteamCMD to install/update game..."
    echo "Using Steam user: ${STEAM_USER}"

    if [ -z "$STEAM_USER" ] || [ -z "$STEAM_PASS" ]; then
        echo "ERROR: STEAM_USER and STEAM_PASS must be provided for first install or forced update"
        kill $XVFB_PID 2>/dev/null || true
        exit 1
    fi

    echo "Running SteamCMD..."
    STEAMCMD_ARGS=(
        +@sSteamCmdForcePlatformType windows
        +force_install_dir "$STEAMAPPDIR"
        +login "$STEAM_USER" "$STEAM_PASS" "$STEAM_GUARD"
        +app_update "$STEAMAPPID"
    )

    if [ -n "${STEAM_BRANCH}" ]; then
        STEAMCMD_ARGS+=(-beta "$STEAM_BRANCH")
    fi

    STEAMCMD_ARGS+=(validate +quit)

    ./steamcmd.sh "${STEAMCMD_ARGS[@]}"

    STEAMCMD_EXIT_CODE=$?
    if [ $STEAMCMD_EXIT_CODE -ne 0 ]; then
        echo "SteamCMD failed with exit code: $STEAMCMD_EXIT_CODE"
        echo "This might be due to:"
        echo "1. Invalid Steam credentials"
        echo "2. Network issues"
        echo "3. Invalid App ID ($STEAMAPPID)"
        echo "4. Platform/architecture mismatch"
        kill $XVFB_PID 2>/dev/null || true
        exit 1
    fi

    echo "Game installation/update completed successfully"
else
    echo "Existing game install detected at ${GAME_EXE_PATH}; skipping SteamCMD login/update."
fi

# Ensure Steamworks redistributables exist for Windows dedicated server auth APIs.
# This commonly provides steamclient64.dll/tier0_s64.dll/vstdlib_s64.dll used by SteamGameServer.Init.
mkdir -p "${STEAMWORKS_REDIST_DIR}"
if should_refresh_steamworks_redist; then
    echo "Installing/updating Steamworks redistributables (app 1007, anonymous)..."
    ./steamcmd.sh +@sSteamCmdForcePlatformType windows \
        +force_install_dir "${STEAMWORKS_REDIST_DIR}" \
        +login anonymous \
        +app_update 1007 validate \
        +quit || true
else
    echo "Existing Steamworks redistributables detected; skipping anonymous refresh."
fi

# Change to the game directory
cd "${STEAMAPPDIR}"

# Re-apply MelonLoader and mod payloads after SteamCMD update/validate.
# Steam validation can overwrite or remove loader files in the game directory.
echo "Applying MelonLoader bootstrap files..."
if [ -f "/home/steam/bootstrap/ml/version.dll" ]; then
    cp -f "/home/steam/bootstrap/ml/version.dll" "${STEAMAPPDIR}/version.dll"
else
    echo "WARNING: Missing /home/steam/bootstrap/ml/version.dll"
fi

if [ -d "/home/steam/bootstrap/ml/MelonLoader" ]; then
    mkdir -p "${STEAMAPPDIR}/MelonLoader"
    cp -r "/home/steam/bootstrap/ml/MelonLoader/." "${STEAMAPPDIR}/MelonLoader/"
else
    echo "WARNING: Missing /home/steam/bootstrap/ml/MelonLoader"
fi

if [ -f "/home/steam/bootstrap/mods/${MOD_DLL_NAME}" ]; then
    mkdir -p "${STEAMAPPDIR}/Mods"
    rm -f "${STEAMAPPDIR}/Mods/DedicatedServerMod_Mono_Server.dll" "${STEAMAPPDIR}/Mods/DedicatedServerMod_Il2cpp_Server.dll"
    cp -f "/home/steam/bootstrap/mods/${MOD_DLL_NAME}" "${STEAMAPPDIR}/Mods/${MOD_DLL_NAME}"
else
    echo "WARNING: Missing bootstrap mod DLL: /home/steam/bootstrap/mods/${MOD_DLL_NAME}"
fi

# Check if the game executable exists
if [ ! -f "${GAME_EXE_PATH}" ]; then
    echo "ERROR: Game executable 'Schedule I.exe' not found in ${STEAMAPPDIR}"
    echo "Available files:"
    ls -la "${STEAMAPPDIR}" || echo "Directory not accessible"
    kill $XVFB_PID 2>/dev/null || true
    exit 1
fi

# Steamworks bootstrap for dedicated server auth/sockets backends.
# The game process must see steam_appid.txt in its working directory.
echo "Writing steam_appid.txt (${STEAMAPPID})"
printf "%s\n" "${STEAMAPPID}" > "${STEAMAPPDIR}/steam_appid.txt"
export SteamAppId="${STEAMAPPID}"
export SteamGameId="${STEAMAPPID}"

# Copy Steamworks redistributable DLLs if available.
copy_steam_dll_if_found() {
    local dll_name="$1"
    local source_file=""

    local candidates=(
        "${STEAMWORKS_REDIST_DIR}/${dll_name}"
        "${STEAMWORKS_REDIST_DIR}/redistributable_bin/${dll_name}"
        "${STEAMWORKS_REDIST_DIR}/redistributable_bin/win64/${dll_name}"
        "/home/steam/steamcmd/${dll_name}"
        "/home/steam/steamcmd/steamapps/common/Steamworks SDK Redist/${dll_name}"
        "/home/steam/steamcmd/steamapps/common/Steamworks SDK Redist/redistributable_bin/${dll_name}"
        "/home/steam/steamcmd/steamapps/common/Steamworks SDK Redist/redistributable_bin/win64/${dll_name}"
        "/home/steam/Steam/steamapps/common/Steamworks SDK Redist/${dll_name}"
        "/home/steam/Steam/steamapps/common/Steamworks SDK Redist/redistributable_bin/${dll_name}"
        "/home/steam/Steam/steamapps/common/Steamworks SDK Redist/redistributable_bin/win64/${dll_name}"
    )

    for candidate in "${candidates[@]}"; do
        if [ -f "$candidate" ]; then
            source_file="$candidate"
            break
        fi
    done

    if [ -z "$source_file" ]; then
        source_file=$(find /home/steam -type f -name "$dll_name" 2>/dev/null | head -n 1)
    fi

    if [ -n "$source_file" ] && [ -f "$source_file" ]; then
        cp -f "$source_file" "${STEAMAPPDIR}/${dll_name}"
        echo "  Copied ${dll_name} from ${source_file}"
    else
        echo "  WARNING: Could not locate ${dll_name}"
    fi
}

echo "Copying Steamworks native DLLs into game directory..."
copy_steam_dll_if_found "steamclient64.dll"
copy_steam_dll_if_found "tier0_s64.dll"
copy_steam_dll_if_found "vstdlib_s64.dll"
copy_steam_dll_if_found "steamclient.dll"
copy_steam_dll_if_found "tier0_s.dll"
copy_steam_dll_if_found "vstdlib_s.dll"

# Prefer native Steam/MelonLoader bridge DLLs under Wine.
BASE_WINE_DLL_OVERRIDES="version=n,b;steam_api64=n,b;steamclient64=n,b;tier0_s64=n,b;vstdlib_s64=n,b;steamclient=n,b;tier0_s=n,b;vstdlib_s=n,b"
if [ -n "${WINEDLLOVERRIDES}" ]; then
    export WINEDLLOVERRIDES="${BASE_WINE_DLL_OVERRIDES};${WINEDLLOVERRIDES}"
else
    export WINEDLLOVERRIDES="${BASE_WINE_DLL_OVERRIDES}"
fi

echo "Steam DLL presence check:"
for required_file in steam_api64.dll steamclient64.dll tier0_s64.dll vstdlib_s64.dll steamclient.dll tier0_s.dll vstdlib_s.dll steam_appid.txt; do
    if [ -f "${STEAMAPPDIR}/${required_file}" ]; then
        echo "  OK: ${required_file}"
    else
        echo "  MISSING: ${required_file}"
    fi
done

# Start the game server with Wine
SERVER_LAUNCH_ARGS=(
    -batchmode
    -nographics
    --dedicated-server
    --require-auth
    --auth-provider steam_game_server
    --messaging-backend steam_networking_sockets
    --steam-gs-anonymous
)

echo "Starting game server with Wine..."
echo "Launch args: ${SERVER_LAUNCH_ARGS[*]}"
echo "WINEDLLOVERRIDES=${WINEDLLOVERRIDES}"
wine "Schedule I.exe" "${SERVER_LAUNCH_ARGS[@]}" &
GAME_PID=$!

# Function to handle shutdown
cleanup() {
    echo "Shutting down..."
    kill $GAME_PID 2>/dev/null || true
    kill $XVFB_PID 2>/dev/null || true
    exit 0
}

# Set up signal handlers
trap cleanup SIGTERM SIGINT

# Wait for the game process
wait $GAME_PID
GAME_EXIT_CODE=$?

echo "Game server exited with code: $GAME_EXIT_CODE"

# Clean up
kill $XVFB_PID 2>/dev/null || true
