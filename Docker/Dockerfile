FROM scottyhardy/docker-wine:stable-8.0.2

ARG MELONLOADER_VERSION=v0.7.2
ARG MELONLOADER_DOWNLOAD_URL=https://github.com/LavaGang/MelonLoader/releases/download/${MELONLOADER_VERSION}/MelonLoader.x64.zip

# Switch to root to install additional packages
USER root

# Install additional dependencies including Xvfb for headless operation
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    wget \
    unzip \
    xz-utils \
    xvfb \
    x11-utils \
    && rm -rf /var/lib/apt/lists/*

# Install Winetricks (if not already present)
RUN if [ ! -f /usr/local/bin/winetricks ]; then \
        wget -O /usr/local/bin/winetricks https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks && \
        chmod +x /usr/local/bin/winetricks; \
    fi

# Create a steam user
RUN useradd -m -s /bin/bash steam && \
    usermod -aG audio,video steam

# Switch to steam user
USER steam
WORKDIR /home/steam

# Install SteamCMD
RUN mkdir /home/steam/steamcmd && \
    wget -qO- https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
    | tar -xz -C /home/steam/steamcmd

# Environment variables for SteamCMD and game
ENV STEAMAPPID=3164500
ENV STEAMAPPDIR=/home/steam/game
ENV WINEDLLOVERRIDES="version=n,b"

# Set up Wine environment for steam user (remove DISPLAY=:0 for headless)
ENV WINEPREFIX=/home/steam/game/wineprefix
ENV WINEARCH=win64

# Clean up any existing Wine prefix and create game directory
RUN rm -rf ${WINEPREFIX} /home/steam/.wine && \
    mkdir -p ${STEAMAPPDIR}

# Initialize Wine prefix and install dependencies with virtual display
RUN export DISPLAY=:99 && \
    Xvfb :99 -screen 0 1024x768x24 -ac +extension GLX +render -noreset & \
    sleep 5 && \
    wineboot --init && \
    sleep 10 && \
    winetricks -q corefonts vcrun2019 && \
    pkill Xvfb || true

# Stage MelonLoader and mod artifacts outside the game directory.
# SteamCMD app_update/validate runs at container start and may overwrite game files,
# so we keep a bootstrap copy and re-apply it in run.sh after updates.
RUN mkdir -p /home/steam/bootstrap/ml /home/steam/bootstrap/mods ${STEAMAPPDIR}/MelonLoader ${STEAMAPPDIR}/Mods

# Download MelonLoader bootstrap files at build time so the local build context
# only needs the server mod DLL from the release package.
RUN wget -O /tmp/MelonLoader.x64.zip "${MELONLOADER_DOWNLOAD_URL}" && \
    unzip -q /tmp/MelonLoader.x64.zip -d /home/steam/bootstrap/ml && \
    rm -f /tmp/MelonLoader.x64.zip

# Copy the release DLL placed next to this Dockerfile by the user.
COPY --chown=steam:steam DedicatedServerMod_Mono_Server.dll /home/steam/bootstrap/mods/DedicatedServerMod_Mono_Server.dll

# Set working directory
WORKDIR ${STEAMAPPDIR}

# Expose game and Steam query ports
EXPOSE 38465/udp
EXPOSE 27016/udp

# Copy and set up the run script
COPY --chown=steam:steam run.sh /home/steam/run.sh
RUN chmod +x /home/steam/run.sh

ENV STEAM_BRANCH=alternate
ENV STEAM_USER=""
ENV STEAM_PASS=""
ENV STEAM_GUARD=""

ENTRYPOINT ["/home/steam/run.sh"]
