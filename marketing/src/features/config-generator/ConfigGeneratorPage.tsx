import { useMemo, useState, type ReactNode } from "react";
import { Copy, Download, ExternalLink } from "lucide-react";
import Header from "@/components/Header";
import Footer from "@/components/Footer";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import {
  DEFAULT_CONFIG_DRAFT,
  type AuthProvider,
  type MessagingBackend,
  type ServerConfigDraft,
  generateServerConfigToml,
} from "./config-generator";

const ConfigGeneratorPage = () => {
  const [draft, setDraft] = useState<ServerConfigDraft>(DEFAULT_CONFIG_DRAFT);
  const [copyState, setCopyState] = useState<"idle" | "copied" | "failed">("idle");
  const toml = useMemo(() => generateServerConfigToml(draft), [draft]);

  const updateDraft = <K extends keyof ServerConfigDraft>(key: K, value: ServerConfigDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }));
  };

  const updateNumber = <K extends keyof ServerConfigDraft>(key: K, value: string) => {
    updateDraft(key, Number(value) as ServerConfigDraft[K]);
  };

  const copyToml = async () => {
    try {
      await navigator.clipboard.writeText(toml);
      setCopyState("copied");
      window.setTimeout(() => setCopyState("idle"), 1800);
    } catch {
      setCopyState("failed");
    }
  };

  const downloadToml = () => {
    const blob = new Blob([toml], { type: "text/plain;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "server_config.toml";
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="min-h-screen overflow-x-hidden bg-[#10110f] text-[#f4eddf]">
      <Header />
      <main className="mx-auto max-w-[1480px] px-4 pb-20 pt-24 md:px-8">
        <section className="grid gap-6 border-b border-[#2b3025] pb-8 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
          <div>
            <h1 className="max-w-none text-[clamp(2.5rem,4.7vw,5.25rem)] font-semibold leading-[0.95] tracking-tight min-[1180px]:whitespace-nowrap">
              Generate server_config.toml
            </h1>
            <p className="mt-5 max-w-2xl text-base leading-7 text-[#b7c9a5] md:text-lg">
              Build a current S1DS config with configurable player slots,{" "}
              <InlineCode>SteamGameServer</InlineCode> auth, safe save-path defaults, and panel-friendly console settings.
            </p>
          </div>
          <div className="flex flex-wrap gap-3">
            <Button onClick={copyToml} className="min-h-11 bg-[#9ac36d] text-[#11140f] hover:bg-[#afd681]">
              <Copy />
              {copyState === "copied" ? "Copied" : "Copy TOML"}
            </Button>
            <Button
              variant="outline"
              onClick={downloadToml}
              className="min-h-11 border-[#3b4235] bg-transparent text-[#f4eddf] hover:bg-[#1b1d18]"
            >
              <Download />
              Download
            </Button>
            <Button
              variant="ghost"
              asChild
              className="min-h-11 text-[#dceccc] hover:bg-[#1b1d18] hover:text-[#f4eddf]"
            >
              <a href="https://docs.s1servers.com/docs/configuration.html" target="_blank" rel="noreferrer">
                Docs
                <ExternalLink />
              </a>
            </Button>
          </div>
        </section>

        {copyState === "failed" && (
          <p className="mt-4 text-sm text-[#ff9f9f]">
            Clipboard access failed. Select the generated TOML manually or use Download.
          </p>
        )}

        <section className="mt-8 grid min-w-0 gap-6 lg:grid-cols-[minmax(0,0.92fr)_minmax(520px,0.8fr)]">
          <div className="grid min-w-0 gap-6">
            <WorkbenchSection
              title="Messaging backend"
              aside="Pick the network path and auth provider you are actually deploying. Use SteamGameServer for public servers."
            >
              <FieldGrid>
                <SelectField
                  label="Messaging backend"
                  value={draft.messagingBackend}
                  onValueChange={(value) => updateDraft("messagingBackend", value as MessagingBackend)}
                  options={[
                    ["FishNetRpc", "FishNetRpc"],
                    ["SteamNetworkingSockets", "SteamNetworkingSockets"],
                  ]}
                />
                <SelectField
                  label="Auth provider"
                  value={draft.authProvider}
                  onValueChange={(value) => updateDraft("authProvider", value as AuthProvider)}
                  options={[
                    ["SteamGameServer", "SteamGameServer"],
                    ["None", "None / local testing"],
                  ]}
                />
              </FieldGrid>
            </WorkbenchSection>

            <WorkbenchSection title="Server" aside="S1DS uses maxPlayers directly for direct-IP capacity.">
              <FieldGrid>
                <TextField label="Server name" value={draft.serverName} onChange={(value) => updateDraft("serverName", value)} />
                <NumberField label="Player slots" value={draft.maxPlayers} min={1} max={64} onChange={(value) => updateNumber("maxPlayers", value)} />
                <NumberField label="Game port" value={draft.serverPort} min={1024} max={65535} onChange={(value) => updateNumber("serverPort", value)} />
                <NumberField label="Steam query port" value={draft.steamGameServerQueryPort} min={1024} max={65535} onChange={(value) => updateNumber("steamGameServerQueryPort", value)} />
              </FieldGrid>
              <TextAreaField
                label="Server description"
                value={draft.serverDescription}
                onChange={(value) => updateDraft("serverDescription", value)}
              />
              <TextField
                label="Save path"
                value={draft.saveGamePath}
                placeholder="Leave empty for UserData/DedicatedServerSave"
                onChange={(value) => updateDraft("saveGamePath", value)}
              />
            </WorkbenchSection>

            <WorkbenchSection title="Operations" aside="Enable only the surfaces you plan to expose or support.">
              <ToggleGrid>
                <ToggleField label="TCP console" checked={draft.tcpConsoleEnabled} onCheckedChange={(value) => updateDraft("tcpConsoleEnabled", value)} />
                <ToggleField label="Web panel" checked={draft.webPanelEnabled} onCheckedChange={(value) => updateDraft("webPanelEnabled", value)} />
                <ToggleField label="Mod verification" checked={draft.modVerificationEnabled} onCheckedChange={(value) => updateDraft("modVerificationEnabled", value)} />
                <ToggleField label="Pause when empty" checked={draft.pauseGameWhenEmpty} onCheckedChange={(value) => updateDraft("pauseGameWhenEmpty", value)} />
              </ToggleGrid>
            </WorkbenchSection>
          </div>

          <aside className="min-w-0 lg:sticky lg:top-24">
            <div className="min-w-0 overflow-hidden rounded-md border border-[#2b3025] bg-[#0c0d0b]">
              <div className="flex items-center justify-between gap-4 border-b border-[#2b3025] px-4 py-3">
                <div>
                  <h2 className="text-sm font-semibold">server_config.toml</h2>
                  <p className="mt-0.5 text-xs text-[#87947c]">Generated locally in your browser.</p>
                </div>
                <div className="flex gap-2">
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={copyToml}
                    className="text-[#dceccc] hover:bg-[#1b1d18] hover:text-[#f4eddf]"
                  >
                    <Copy />
                    Copy
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={downloadToml}
                    className="text-[#dceccc] hover:bg-[#1b1d18] hover:text-[#f4eddf]"
                  >
                    <Download />
                    Save
                  </Button>
                </div>
              </div>
              <pre className="max-h-[calc(100dvh-11rem)] min-h-[620px] max-w-full overflow-auto p-5 text-left font-mono text-[12px] leading-5 text-[#b8c7ac]">
                <code>{toml}</code>
              </pre>
            </div>
          </aside>
        </section>
      </main>
      <Footer />
    </div>
  );
};

type WorkbenchSectionProps = {
  title: string;
  aside: string;
  children: ReactNode;
};

const WorkbenchSection = ({ title, aside, children }: WorkbenchSectionProps) => (
  <section className="min-w-0 border-t border-[#30352d] pt-5">
    <div className="mb-4 grid gap-2 md:grid-cols-[180px_minmax(0,1fr)]">
      <h2 className="text-sm font-semibold text-[#f4eddf]">{title}</h2>
      <p className="min-w-0 max-w-2xl text-sm leading-6 text-[#87947c]">{aside}</p>
    </div>
    <div className="grid min-w-0 gap-5 md:pl-[180px]">{children}</div>
  </section>
);

const FieldGrid = ({ children }: { children: ReactNode }) => (
  <div className="grid gap-4 md:grid-cols-2">{children}</div>
);

const ToggleGrid = ({ children }: { children: ReactNode }) => (
  <div className="grid gap-3 sm:grid-cols-2">{children}</div>
);

type TextFieldProps = {
  label: string;
  value: string;
  placeholder?: string;
  onChange: (value: string) => void;
};

const TextField = ({ label, value, placeholder, onChange }: TextFieldProps) => (
  <div className="grid gap-2">
    <FieldLabel>{label}</FieldLabel>
    <Input
      value={value}
      placeholder={placeholder}
      onChange={(event) => onChange(event.target.value)}
      className={inputClassName}
    />
  </div>
);

const TextAreaField = ({ label, value, onChange }: TextFieldProps) => (
  <div className="grid gap-2">
    <FieldLabel>{label}</FieldLabel>
    <Textarea value={value} onChange={(event) => onChange(event.target.value)} className={inputClassName} />
  </div>
);

type NumberFieldProps = {
  label: string;
  value: number;
  min: number;
  max: number;
  onChange: (value: string) => void;
};

const NumberField = ({ label, value, min, max, onChange }: NumberFieldProps) => (
  <div className="grid gap-2">
    <FieldLabel>{label}</FieldLabel>
    <Input
      type="number"
      min={min}
      max={max}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      className={inputClassName}
    />
  </div>
);

type SelectFieldProps = {
  label: string;
  value: string;
  options: [string, string][];
  onValueChange: (value: string) => void;
};

const SelectField = ({ label, value, options, onValueChange }: SelectFieldProps) => (
  <div className="grid gap-2">
    <FieldLabel>{label}</FieldLabel>
    <Select value={value} onValueChange={onValueChange}>
      <SelectTrigger className={inputClassName}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {options.map(([optionValue, optionLabel]) => (
          <SelectItem key={optionValue} value={optionValue}>
            {optionLabel}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  </div>
);

type ToggleFieldProps = {
  label: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
};

const ToggleField = ({ label, checked, onCheckedChange }: ToggleFieldProps) => (
  <label className="flex min-h-12 items-center justify-between gap-4 rounded-md border border-[#30352d] bg-[#141611] px-4 py-3 text-sm text-[#e8f2dc]">
    <span>{label}</span>
    <Switch checked={checked} onCheckedChange={onCheckedChange} />
  </label>
);

const FieldLabel = ({ children }: { children: ReactNode }) => (
  <Label className="text-sm font-medium text-[#dceccc]">{children}</Label>
);

const inputClassName =
  "min-h-11 border-[#30352d] bg-[#10110f] text-[#f4eddf] placeholder:text-[#65705f] focus-visible:ring-[#9ac36d]";

const InlineCode = ({ children }: { children: ReactNode }) => (
  <code className="rounded border border-[#30352d] bg-[#141611] px-1.5 py-0.5 font-mono text-[0.9em] text-[#f4eddf]">
    {children}
  </code>
);

export default ConfigGeneratorPage;
