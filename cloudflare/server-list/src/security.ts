const encoder = new TextEncoder();

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
