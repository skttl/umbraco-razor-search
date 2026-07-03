import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { client as backendApiClient } from "@umbraco-cms/backoffice/external/backend-api";
import { UmbApiInterceptorController, tryExecute } from "@umbraco-cms/backoffice/resources";

interface ManagementApiClient {
  get<T>(options: Record<string, unknown>): Promise<T>;
  post<T>(options: Record<string, unknown>): Promise<T>;
}

interface TryExecuteResult<T> {
  data?: T | null;
  error?: unknown;
}

export interface RazorSearchDocumentStatusResponse {
  documentId: string;
  state: string;
  includeDescendants: boolean;
  pendingDocumentCount: number;
  completedDocumentCount: number;
  failedDocumentCount: number;
  updatedAt: string;
  message?: string;
}

export interface RazorSearchQueueStatusResponse {
  state: string;
  totalJobCount: number;
  pendingJobCount: number;
  runningJobCount: number;
  completedJobCount: number;
  failedJobCount: number;
  cancelledJobCount: number;
  progressPercent: number;
  updatedAt: string;
  message?: string;
}

export interface QueueRazorSearchDocumentResponse {
  documentId: string;
  scope: string;
  includeDescendants: boolean;
  state: string;
  maxDocumentCount: number;
  discoveredDocumentCount: number;
  processedDocumentCount: number;
  queuedRouteCount: number;
  duplicateRouteCount: number;
  skippedDocumentCount: number;
  wasTruncated: boolean;
  queuedAt: string;
  message: string;
  status: RazorSearchDocumentStatusResponse;
}

interface QueueRazorSearchDocumentRequest {
  includeDescendants: boolean;
}

export interface QueueRazorSearchPublishedContentResponse {
  scope: string;
  state: string;
  maxDocumentCount: number;
  discoveredDocumentCount: number;
  processedDocumentCount: number;
  queuedRouteCount: number;
  duplicateRouteCount: number;
  skippedDocumentCount: number;
  wasTruncated: boolean;
  queuedAt: string;
  message: string;
}

export const RAZOR_SEARCH_MANAGEMENT_ENDPOINTS = {
  documentStatus: "/umbraco/management/api/v1/razor-search/document/{id}/status",
  queueDocument: "/umbraco/management/api/v1/razor-search/document/{id}/queue",
  queueStatus: "/umbraco/management/api/v1/razor-search/queue/status",
  rebuildPublishedContent:
    "/umbraco/management/api/v1/razor-search/published/rebuild",
} as const;

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null;
}

function readString(source: Record<string, unknown>, ...keys: string[]): string | undefined {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === "string") {
      return value;
    }
  }

  return undefined;
}

function readBoolean(source: Record<string, unknown>, ...keys: string[]): boolean | undefined {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === "boolean") {
      return value;
    }
  }

  return undefined;
}

function readNumber(source: Record<string, unknown>, ...keys: string[]): number | undefined {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === "number") {
      return value;
    }
  }

  return undefined;
}

function unwrapTryExecuteData<T>(response: unknown): T | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const maybeResult = response as TryExecuteResult<T>;
  if (maybeResult.error) {
    return undefined;
  }

  if ("data" in response) {
    return maybeResult.data ?? undefined;
  }

  return response as T;
}

function normalizeDocumentStatusResponse(
  response: unknown,
): RazorSearchDocumentStatusResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const state = readString(response, "state", "State");
  const documentId = readString(response, "documentId", "DocumentId");
  const updatedAt = readString(response, "updatedAt", "UpdatedAt");

  if (!state || !documentId || !updatedAt) {
    return undefined;
  }

  return {
    documentId,
    state,
    includeDescendants:
      readBoolean(response, "includeDescendants", "IncludeDescendants") ?? false,
    pendingDocumentCount:
      readNumber(response, "pendingDocumentCount", "PendingDocumentCount") ?? 0,
    completedDocumentCount:
      readNumber(response, "completedDocumentCount", "CompletedDocumentCount") ?? 0,
    failedDocumentCount:
      readNumber(response, "failedDocumentCount", "FailedDocumentCount") ?? 0,
    updatedAt,
    message: readString(response, "message", "Message"),
  };
}

function normalizeQueueDocumentResponse(
  response: unknown,
): QueueRazorSearchDocumentResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const documentId = readString(response, "documentId", "DocumentId");
  const scope = readString(response, "scope", "Scope");
  const state = readString(response, "state", "State");
  const queuedAt = readString(response, "queuedAt", "QueuedAt");
  const message = readString(response, "message", "Message");
  const status = normalizeDocumentStatusResponse(response.status ?? response.Status);

  if (!documentId || !scope || !state || !queuedAt || !message || !status) {
    return undefined;
  }

  return {
    documentId,
    scope,
    includeDescendants:
      readBoolean(response, "includeDescendants", "IncludeDescendants") ?? false,
    state,
    maxDocumentCount: readNumber(response, "maxDocumentCount", "MaxDocumentCount") ?? 0,
    discoveredDocumentCount:
      readNumber(response, "discoveredDocumentCount", "DiscoveredDocumentCount") ?? 0,
    processedDocumentCount:
      readNumber(response, "processedDocumentCount", "ProcessedDocumentCount") ?? 0,
    queuedRouteCount: readNumber(response, "queuedRouteCount", "QueuedRouteCount") ?? 0,
    duplicateRouteCount:
      readNumber(response, "duplicateRouteCount", "DuplicateRouteCount") ?? 0,
    skippedDocumentCount:
      readNumber(response, "skippedDocumentCount", "SkippedDocumentCount") ?? 0,
    wasTruncated: readBoolean(response, "wasTruncated", "WasTruncated") ?? false,
    queuedAt,
    message,
    status,
  };
}

function normalizeQueueStatusResponse(
  response: unknown,
): RazorSearchQueueStatusResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const state = readString(response, "state", "State");
  const updatedAt = readString(response, "updatedAt", "UpdatedAt");

  if (!state || !updatedAt) {
    return undefined;
  }

  return {
    state,
    totalJobCount: readNumber(response, "totalJobCount", "TotalJobCount") ?? 0,
    pendingJobCount: readNumber(response, "pendingJobCount", "PendingJobCount") ?? 0,
    runningJobCount: readNumber(response, "runningJobCount", "RunningJobCount") ?? 0,
    completedJobCount:
      readNumber(response, "completedJobCount", "CompletedJobCount") ?? 0,
    failedJobCount: readNumber(response, "failedJobCount", "FailedJobCount") ?? 0,
    cancelledJobCount:
      readNumber(response, "cancelledJobCount", "CancelledJobCount") ?? 0,
    progressPercent:
      readNumber(response, "progressPercent", "ProgressPercent") ?? 0,
    updatedAt,
    message: readString(response, "message", "Message"),
  };
}

function normalizeQueuePublishedContentResponse(
  response: unknown,
): QueueRazorSearchPublishedContentResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const scope = readString(response, "scope", "Scope");
  const state = readString(response, "state", "State");
  const queuedAt = readString(response, "queuedAt", "QueuedAt");
  const message = readString(response, "message", "Message");

  if (!scope || !state || !queuedAt || !message) {
    return undefined;
  }

  return {
    scope,
    state,
    maxDocumentCount: readNumber(response, "maxDocumentCount", "MaxDocumentCount") ?? 0,
    discoveredDocumentCount:
      readNumber(response, "discoveredDocumentCount", "DiscoveredDocumentCount") ?? 0,
    processedDocumentCount:
      readNumber(response, "processedDocumentCount", "ProcessedDocumentCount") ?? 0,
    queuedRouteCount: readNumber(response, "queuedRouteCount", "QueuedRouteCount") ?? 0,
    duplicateRouteCount:
      readNumber(response, "duplicateRouteCount", "DuplicateRouteCount") ?? 0,
    skippedDocumentCount:
      readNumber(response, "skippedDocumentCount", "SkippedDocumentCount") ?? 0,
    wasTruncated: readBoolean(response, "wasTruncated", "WasTruncated") ?? false,
    queuedAt,
    message,
  };
}

export class RazorSearchManagementClient extends UmbControllerBase {
  readonly #apiInterceptor = new UmbApiInterceptorController(this);
  #interceptorsBound = false;

  constructor(host: UmbControllerHost) {
    super(host);
  }

  async queueDocument(
    documentId: string,
    includeDescendants: boolean,
  ): Promise<QueueRazorSearchDocumentResponse | undefined> {
    const client = await this.#createClient();
    if (!client) {
      return undefined;
    }

    const response = await tryExecute(
      this,
      client.post<QueueRazorSearchDocumentResponse>({
        security: [{ scheme: "bearer", type: "http" }],
        url: RAZOR_SEARCH_MANAGEMENT_ENDPOINTS.queueDocument,
        path: { id: documentId },
        body: { includeDescendants } satisfies QueueRazorSearchDocumentRequest,
        headers: {
          "Content-Type": "application/json",
        },
      }),
    );

    return normalizeQueueDocumentResponse(
      unwrapTryExecuteData<QueueRazorSearchDocumentResponse>(response),
    );
  }

  async getDocumentStatus(
    documentId: string,
  ): Promise<RazorSearchDocumentStatusResponse | undefined> {
    const client = await this.#createClient();
    if (!client) {
      return undefined;
    }

    const response = await tryExecute(
      this,
      client.get<RazorSearchDocumentStatusResponse>({
        security: [{ scheme: "bearer", type: "http" }],
        url: RAZOR_SEARCH_MANAGEMENT_ENDPOINTS.documentStatus,
        path: { id: documentId },
      }),
    );

    return normalizeDocumentStatusResponse(
      unwrapTryExecuteData<RazorSearchDocumentStatusResponse>(response),
    );
  }

  async getQueueStatus(): Promise<RazorSearchQueueStatusResponse | undefined> {
    const client = await this.#createClient();
    if (!client) {
      return undefined;
    }

    const response = await tryExecute(
      this,
      client.get<RazorSearchQueueStatusResponse>({
        security: [{ scheme: "bearer", type: "http" }],
        url: RAZOR_SEARCH_MANAGEMENT_ENDPOINTS.queueStatus,
      }),
    );

    return normalizeQueueStatusResponse(
      unwrapTryExecuteData<RazorSearchQueueStatusResponse>(response),
    );
  }

  async backfillPublishedContent(): Promise<
    QueueRazorSearchPublishedContentResponse | undefined
  > {
    const client = await this.#createClient();
    if (!client) {
      return undefined;
    }

    const response = await tryExecute(
      this,
      client.post<QueueRazorSearchPublishedContentResponse>({
        security: [{ scheme: "bearer", type: "http" }],
        url: RAZOR_SEARCH_MANAGEMENT_ENDPOINTS.rebuildPublishedContent,
        body: {},
        headers: {
          "Content-Type": "application/json",
        },
      }),
    );

    return normalizeQueuePublishedContentResponse(
      unwrapTryExecuteData<QueueRazorSearchPublishedContentResponse>(response),
    );
  }

  async #createClient(): Promise<ManagementApiClient | undefined> {
    const authContext = await this.getContext(UMB_AUTH_CONTEXT, {
      preventTimeout: true,
    });

    if (!authContext) {
      return undefined;
    }

    const openApi = authContext.getOpenApiConfiguration();
    backendApiClient.setConfig({
      baseUrl: openApi.base ?? "",
      credentials: openApi.credentials ?? "same-origin",
      auth: () => authContext.getLatestToken(),
    });

    if (!this.#interceptorsBound) {
      this.#apiInterceptor.bindDefaultInterceptors(
        backendApiClient as Parameters<UmbApiInterceptorController["bindDefaultInterceptors"]>[0],
      );
      this.#interceptorsBound = true;
    }

    return backendApiClient as unknown as ManagementApiClient;
  }
}
