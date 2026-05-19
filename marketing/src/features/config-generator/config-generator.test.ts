import { describe, expect, it } from "vitest";
import {
  DEFAULT_CONFIG_DRAFT,
  generateServerConfigToml,
} from "./config-generator";

describe("generateServerConfigToml", () => {
  it("generates current S1DS player capacity and SteamGameServer defaults", () => {
    const toml = generateServerConfigToml(DEFAULT_CONFIG_DRAFT);

    expect(toml).toContain("[server]");
    expect(toml).toContain("maxPlayers = 16");
    expect(toml).toContain("authProvider = 'SteamGameServer'");
    expect(toml).toContain("saveGamePath = ''");
    expect(toml).not.toContain("SteamWebApi");
  });

  it("escapes TOML literal strings and clamps unsafe numeric ranges", () => {
    const toml = generateServerConfigToml({
      ...DEFAULT_CONFIG_DRAFT,
      serverName: "Bars' Server",
      maxPlayers: 999,
      serverPort: 80,
    });

    expect(toml).toContain('serverName = "Bars\' Server"');
    expect(toml).toContain("maxPlayers = 64");
    expect(toml).toContain("serverPort = 1024");
  });
});
