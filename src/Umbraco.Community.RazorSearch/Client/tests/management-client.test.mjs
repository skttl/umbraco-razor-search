import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const source = readFileSync(new URL("../src/razor-search-management.client.ts", import.meta.url), "utf8");
const { outputText } = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
});
const status = { state: "idle", updatedAt: "2026-11-12T12:00:00Z" };

function fixture(fetch) {
  let configuredClient;
  const backendClient = { get: async () => ({ data: status }) };
  // Umbraco 19 exposes cookie configuration, with no token accessor.
  const auth = {
    configureClient(client) { configuredClient = client; },
    getOpenApiConfiguration() { return { base: "https://cms.example/" }; },
  };
  const imports = {
    "@umbraco-cms/backoffice/auth": { UMB_AUTH_CONTEXT: Symbol() },
    "@umbraco-cms/backoffice/class-api": {
      UmbControllerBase: class { async getContext() { return auth; } },
    },
    "@umbraco-cms/backoffice/external/backend-api": { client: backendClient },
    "@umbraco-cms/backoffice/resources": { tryExecute: async (_, response) => response },
  };
  const exports = {};
  runInNewContext(outputText, {
    exports,
    require(id) {
      assert.ok(id in imports, `Unexpected import: ${id}`);
      return imports[id];
    },
    URL, AbortController, TextDecoder, fetch,
  });
  return {
    client: new exports.RazorSearchManagementClient({}),
    assertConfigured() { assert.equal(configuredClient, backendClient); },
  };
}

test("management requests use the Umbraco 19 client configuration without a token accessor", async () => {
  const { client, assertConfigured } = fixture();
  const result = await client.getQueueStatus();
  assertConfigured();
  assert.equal(result.state, "idle");
});

test("queue stream sends cookies without a bearer token and delivers status events", async () => {
  let request;
  const { client } = fixture(async (url, options) => {
    request = { url, ...options };
    return new Response(`event: queue-status\ndata: ${JSON.stringify(status)}\n\n`);
  });
  const received = [];
  let handle;
  await new Promise((resolve, reject) => {
    client.createQueueStatusStream(
      (value) => received.push(value),
      (error) => error ? reject(error) : resolve(),
    ).then((value) => { handle = value; }, reject);
  });
  assert.equal(new URL(request.url).origin, "https://cms.example");
  assert.equal(request.credentials, "include");
  assert.equal(request.headers.Accept, "text/event-stream");
  assert.equal(request.headers.Authorization, undefined);
  assert.equal(received.length, 1);
  assert.equal(received[0].state, "idle");
  handle.close();
  assert.equal(request.signal.aborted, true);
});
