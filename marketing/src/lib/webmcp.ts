type WebMcpTool = {
  name: string;
  description: string;
  inputSchema: Record<string, unknown>;
  execute: () => unknown;
};

type ModelContextProvider = {
  provideContext?: (context: { tools: WebMcpTool[] }) => void;
};

declare global {
  interface Navigator {
    modelContext?: ModelContextProvider;
  }
}

const navigateTo = (url: string) => {
  window.location.assign(url);
  return { url };
};

export function registerWebMcpTools() {
  const provider = navigator.modelContext;
  if (!provider?.provideContext) {
    return;
  }

  provider.provideContext({
    tools: [
      {
        name: "open_config_generator",
        description: "Open the S1DedicatedServers Schedule I server configuration generator.",
        inputSchema: {
          type: "object",
          properties: {},
          additionalProperties: false,
        },
        execute: () => navigateTo("/config-generator"),
      },
      {
        name: "open_documentation",
        description: "Open the S1DedicatedServers setup and hosting documentation.",
        inputSchema: {
          type: "object",
          properties: {},
          additionalProperties: false,
        },
        execute: () => navigateTo("https://docs.s1servers.com/"),
      },
      {
        name: "open_latest_release",
        description: "Open the latest S1DedicatedServers release downloads on GitHub.",
        inputSchema: {
          type: "object",
          properties: {},
          additionalProperties: false,
        },
        execute: () => navigateTo("https://github.com/ifBars/S1DedicatedServers/releases"),
      },
    ],
  });
}
