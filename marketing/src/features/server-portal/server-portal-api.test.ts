import { describe, expect, it } from "vitest";
import { authUrl, buildPublicListingToml } from "./server-portal-api";

describe("server portal helpers", () => {
  it("builds the exact opt-in TOML block", () => {
    expect(buildPublicListingToml({ listingId: "listing-id", secret: "listing-secret" })).toBe(
      '[publicListing]\npublicListingEnabled = true\npublicListingId = "listing-id"\npublicListingSecret = "listing-secret"',
    );
  });

  it("uses the account provider start route", () => {
    expect(authUrl("steam")).toMatch(/\/api\/v2\/portal\/auth\/steam\/start$/);
  });
});
