import { readFileSync } from "node:fs";
import { defineConfig, type Plugin } from "vite";

const packageManifestTemplatePath = new URL("./public/umbraco-package.json", import.meta.url);
const packageVersion =
  process.env.RELEASE_VERSION
  ?? process.env.npm_package_version
  ?? "0.1.0";
const manifestAssetVersion =
  process.env.RELEASE_VERSION
  ?? `${packageVersion}-dev-${Date.now()}`;

function appendVersionQuery(url: string, version: string): string {
  const parsedUrl = new URL(url, "https://example.local");
  parsedUrl.searchParams.set("v", version);
  return `${parsedUrl.pathname}${parsedUrl.search}${parsedUrl.hash}`;
}

function updateManifestScripts(value: unknown, version: string): unknown {
  if (Array.isArray(value)) {
    return value.map((entry) => updateManifestScripts(entry, version));
  }

  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value).map(([key, entryValue]) => {
        if (key === "js") {
          if (typeof entryValue === "string") {
            return [key, appendVersionQuery(entryValue, version)];
          }

          if (Array.isArray(entryValue)) {
            return [
              key,
              entryValue.map((item) =>
                typeof item === "string" ? appendVersionQuery(item, version) : item,
              ),
            ];
          }
        }

        return [key, updateManifestScripts(entryValue, version)];
      }),
    );
  }

  return value;
}

function umbracoPackageManifestPlugin(version: string): Plugin {
  return {
    name: "umbraco-package-manifest",
    apply: "build",
    generateBundle() {
      const manifestTemplate = JSON.parse(readFileSync(packageManifestTemplatePath, "utf8"));
      const manifest = updateManifestScripts(
        {
          ...manifestTemplate,
          version,
        },
        version,
      );

      this.emitFile({
        type: "asset",
        fileName: "umbraco-package.json",
        source: `${JSON.stringify(manifest, null, 2)}\n`,
      });
    },
  };
}

export default defineConfig({
  publicDir: false,
  plugins: [umbracoPackageManifestPlugin(manifestAssetVersion)],
  build: {
    lib: {
      entry: "src/bundle.manifests.ts",
      formats: ["es"],
      fileName: "razor-search-bundle",
    },
    outDir: "../wwwroot/App_Plugins/Umbraco.Community.RazorSearch",
    emptyOutDir: true,
    sourcemap: true,
    rollupOptions: {
      external: [/^@umbraco/],
    },
  },
});
