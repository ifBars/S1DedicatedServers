import { readFile, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, "..");
const distDir = path.join(projectRoot, "dist");
const ssrDir = path.join(projectRoot, "dist-ssr");
const templatePath = path.join(distDir, "index.html");
const serverEntryPath = path.join(ssrDir, "entry-server.js");

const template = await readFile(templatePath, "utf8");
const { render } = await import(pathToFileURL(serverEntryPath).href);
const appHtml = render("/");
const output = template.replace('<div id="root"></div>', `<div id="root">${appHtml}</div>`);

await writeFile(templatePath, output, "utf8");
await rm(ssrDir, { force: true, recursive: true });
