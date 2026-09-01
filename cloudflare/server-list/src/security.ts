const encoder = new TextEncoder();
type TimingSafeSubtleCrypto = SubtleCrypto & {
  timingSafeEqual(left: ArrayBuffer | ArrayBufferView, right: ArrayBuffer | ArrayBufferView): boolean;
};

export function generateSecret(byteLength = 32): string {
  const bytes = crypto.getRandomValues(new Uint8Array(byteLength));
  let binary = "";
  for (const byte of bytes) {
    binary += String.fromCharCode(byte);
  }

  return btoa(binary).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
}

export async function sha256Hex(value: string): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", encoder.encode(value));
  return Array.from(new Uint8Array(digest), (byte) => byte.toString(16).padStart(2, "0")).join("");
}

export async function secureEqual(left: string, right: string): Promise<boolean> {
  const [leftHash, rightHash] = await Promise.all([
    crypto.subtle.digest("SHA-256", encoder.encode(left)),
    crypto.subtle.digest("SHA-256", encoder.encode(right)),
  ]);
  // workerd exposes timingSafeEqual; Bun's ambient SubtleCrypto type currently omits the extension.
  return (crypto.subtle as TimingSafeSubtleCrypto).timingSafeEqual(leftHash, rightHash);
}

export function readBearerSecret(request: Request): string | null {
  const authorization = request.headers.get("Authorization");
  if (!authorization?.startsWith("Bearer ")) {
    return null;
  }

  const secret = authorization.slice("Bearer ".length).trim();
  return secret.length >= 32 && secret.length <= 256 ? secret : null;
}

export function getConnectingIp(request: Request): string | null {
  const value = request.headers.get("CF-Connecting-IP")?.trim();
  if (!value || value.length > 64 || /[\s,/]/.test(value)) {
    return null;
  }

  return value;
}
