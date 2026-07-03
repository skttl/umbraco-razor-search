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

interface ManagementAuthContext {
  getLatestToken(): Promise<string>;
  getOpenApiConfiguration(): {
    base?: string;
    credentials?: RequestCredentials;
  };
}

export interface RazorSearchQueueStatusStreamHandle {
  close(): void;
}

export interface RazorSearchDocumentStatusResponse {
  documentId: string;
  documentName?: string;
  state: string;
  includeDescendants: boolean;
  pendingDocumentCount: number;
  completedDocumentCount: number;
  failedDocumentCount: number;
  updatedAt: string;
  message?: string;
  jobs: RazorSearchQueueJobResponse[];
  snapshots: RazorSearchDocumentSnapshotResponse[];
  indexedEntries: RazorSearchDocumentIndexEntryResponse[];
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
  currentJob?: RazorSearchQueueJobResponse;
}

export interface RazorSearchQueueBatchDetailsResponse {
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
  jobs: RazorSearchQueueJobResponse[];
}

export interface RazorSearchQueueJobResponse {
  documentId: string;
  documentName?: string;
  state: string;
  route: string;
  renderer: string;
  culture?: string;
  segment?: string;
  enqueuedAt: string;
  updatedAt: string;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
}

export interface RazorSearchDocumentSnapshotResponse {
  route: string;
  finalUrl?: string;
  renderer: string;
  culture?: string;
  segment?: string;
  state: string;
  snapshot: string;
  snapshotHtml?: string;
  titleText?: string;
  summaryText?: string;
  headingText?: string;
  bodyText?: string;
  errorMessage?: string;
  renderedAt?: string;
  updatedAt: string;
}

export interface RazorSearchDocumentIndexEntryResponse {
  culture?: string;
  segment?: string;
  titles: string[];
  summaries: string[];
  headings: string[];
  content: string[];
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
  queueBatchDetails: "/umbraco/management/api/v1/razor-search/queue/batch",
  queueStatusStream: "/umbraco/management/api/v1/razor-search/queue/stream",
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

function readStringArray(
  source: Record<string, unknown>,
  ...keys: string[]
): string[] {
  for (const key of keys) {
    const value = source[key];
    if (Array.isArray(value)) {
      return value.filter((item): item is string => typeof item === "string");
    }
  }

  return [];
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
    documentName: readString(response, "documentName", "DocumentName"),
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
    jobs: normalizeQueueJobs(response.jobs ?? response.Jobs),
    snapshots: normalizeDocumentSnapshots(response.snapshots ?? response.Snapshots),
    indexedEntries: normalizeDocumentIndexEntries(
      response.indexedEntries ?? response.IndexedEntries,
    ),
  };
}

function normalizeQueueJobs(response: unknown): RazorSearchQueueJobResponse[] {
  if (!Array.isArray(response)) {
    return [];
  }

  return response
    .map((item) => normalizeQueueJobResponse(item))
    .filter((item): item is RazorSearchQueueJobResponse => item !== undefined);
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
    currentJob: normalizeQueueJobResponse(response.currentJob ?? response.CurrentJob),
  };
}

function normalizeQueueBatchDetailsResponse(
  response: unknown,
): RazorSearchQueueBatchDetailsResponse | undefined {
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
    jobs: normalizeQueueJobs(response.jobs ?? response.Jobs),
  };
}

function normalizeQueueJobResponse(
  response: unknown,
): RazorSearchQueueJobResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const documentId = readString(response, "documentId", "DocumentId");
  const state = readString(response, "state", "State");
  const route = readString(response, "route", "Route");
  const renderer = readString(response, "renderer", "Renderer");
  const enqueuedAt = readString(response, "enqueuedAt", "EnqueuedAt");
  const updatedAt = readString(response, "updatedAt", "UpdatedAt");

  if (!documentId || !state || !route || !renderer || !enqueuedAt || !updatedAt) {
    return undefined;
  }

  return {
    documentId,
    documentName: readString(response, "documentName", "DocumentName"),
    state,
    route,
    renderer,
    culture: readString(response, "culture", "Culture"),
    segment: readString(response, "segment", "Segment"),
    enqueuedAt,
    updatedAt,
    startedAt: readString(response, "startedAt", "StartedAt"),
    completedAt: readString(response, "completedAt", "CompletedAt"),
    errorMessage: readString(response, "errorMessage", "ErrorMessage"),
  };
}

function normalizeDocumentSnapshots(
  response: unknown,
): RazorSearchDocumentSnapshotResponse[] {
  if (!Array.isArray(response)) {
    return [];
  }

  return response
    .map((item) => normalizeDocumentSnapshotResponse(item))
    .filter((item): item is RazorSearchDocumentSnapshotResponse => item !== undefined);
}

function normalizeDocumentSnapshotResponse(
  response: unknown,
): RazorSearchDocumentSnapshotResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  const route = readString(response, "route", "Route");
  const renderer = readString(response, "renderer", "Renderer");
  const state = readString(response, "state", "State");
  const snapshot = readString(response, "snapshot", "Snapshot");
  const updatedAt = readString(response, "updatedAt", "UpdatedAt");

  if (!route || !renderer || !state || !snapshot || !updatedAt) {
    return undefined;
  }

  return {
    route,
    finalUrl: readString(response, "finalUrl", "FinalUrl"),
    renderer,
    culture: readString(response, "culture", "Culture"),
    segment: readString(response, "segment", "Segment"),
    state,
    snapshot,
    snapshotHtml: readString(response, "snapshotHtml", "SnapshotHtml"),
    titleText: readString(response, "titleText", "TitleText"),
    summaryText: readString(response, "summaryText", "SummaryText"),
    headingText: readString(response, "headingText", "HeadingText"),
    bodyText: readString(response, "bodyText", "BodyText"),
    errorMessage: readString(response, "errorMessage", "ErrorMessage"),
    renderedAt: readString(response, "renderedAt", "RenderedAt"),
    updatedAt,
  };
}

function normalizeDocumentIndexEntries(
  response: unknown,
): RazorSearchDocumentIndexEntryResponse[] {
  if (!Array.isArray(response)) {
    return [];
  }

  return response
    .map((item) => normalizeDocumentIndexEntryResponse(item))
    .filter((item): item is RazorSearchDocumentIndexEntryResponse => item !== undefined);
}

function normalizeDocumentIndexEntryResponse(
  response: unknown,
): RazorSearchDocumentIndexEntryResponse | undefined {
  if (!isObject(response)) {
    return undefined;
  }

  return {
    culture: readString(response, "culture", "Culture"),
    segment: readString(response, "segment", "Segment"),
    titles: readStringArray(response, "titles", "Titles"),
    summaries: readStringArray(response, "summaries", "Summaries"),
    headings: readStringArray(response, "headings", "Headings"),
    content: readStringArray(response, "content", "Content"),
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

  async getQueueBatchDetails(): Promise<
    RazorSearchQueueBatchDetailsResponse | undefined
  > {
    const client = await this.#createClient();
    if (!client) {
      return undefined;
    }

    const response = await tryExecute(
      this,
      client.get<RazorSearchQueueBatchDetailsResponse>({
        security: [{ scheme: "bearer", type: "http" }],
        url: RAZOR_SEARCH_MANAGEMENT_ENDPOINTS.queueBatchDetails,
      }),
    );

    return normalizeQueueBatchDetailsResponse(
      unwrapTryExecuteData<RazorSearchQueueBatchDetailsResponse>(response),
    );
  }

  async createQueueStatusStream(
    onStatus: (status: RazorSearchQueueStatusResponse) => void,
    onDisconnect?: (error?: unknown) => void,
  ): Promise<RazorSearchQueueStatusStreamHandle | undefined> {
    const authContext = await this.#getAuthContext();

    if (!authContext || typeof fetch === "undefined") {
      return undefined;
    }

    const openApi = authContext.getOpenApiConfiguration();
    const baseUrl = openApi.base ?? window.location.origin;
    const streamUrl = new URL(
      RAZOR_SEARCH_MANAGEMENT_ENDPOINTS.queueStatusStream,
      baseUrl,
    ).toString();

    const abortController = new AbortController();

    void this.#consumeQueueStatusStream(
      streamUrl,
      authContext,
      abortController.signal,
      onStatus,
      onDisconnect,
    );

    return {
      close() {
        abortController.abort();
      },
    };
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
    const authContext = await this.#getAuthContext();

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

  async #getAuthContext(): Promise<ManagementAuthContext | undefined> {
    const authContext = await this.getContext(UMB_AUTH_CONTEXT, {
      preventTimeout: true,
    });

    return authContext as ManagementAuthContext | undefined;
  }

  async #consumeQueueStatusStream(
    streamUrl: string,
    authContext: ManagementAuthContext,
    signal: AbortSignal,
    onStatus: (status: RazorSearchQueueStatusResponse) => void,
    onDisconnect?: (error?: unknown) => void,
  ) {
    try {
      const token = await authContext.getLatestToken();
      const response = await fetch(streamUrl, {
        method: "GET",
        credentials: "same-origin",
        headers: {
          Accept: "text/event-stream",
          Authorization: token ? `Bearer ${token}` : "",
        },
        signal,
      });

      if (!response.ok || !response.body) {
        throw new Error(`Queue stream request failed with status ${response.status}.`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";

      while (!signal.aborted) {
        const result = await reader.read();
        if (result.done) {
          break;
        }

        buffer += decoder.decode(result.value, { stream: true });
        buffer = this.#drainQueueStatusEvents(buffer, onStatus);
      }

      buffer += decoder.decode();
      this.#drainQueueStatusEvents(buffer, onStatus);

      if (!signal.aborted) {
        onDisconnect?.();
      }
    } catch (error) {
      if (signal.aborted) {
        return;
      }

      onDisconnect?.(error);
    }
  }

  #drainQueueStatusEvents(
    buffer: string,
    onStatus: (status: RazorSearchQueueStatusResponse) => void,
  ) {
    let separatorIndex = buffer.indexOf("\n\n");

    while (separatorIndex >= 0) {
      const rawEvent = buffer.slice(0, separatorIndex);
      buffer = buffer.slice(separatorIndex + 2);
      this.#processQueueStatusEvent(rawEvent, onStatus);
      separatorIndex = buffer.indexOf("\n\n");
    }

    return buffer;
  }

  #processQueueStatusEvent(
    rawEvent: string,
    onStatus: (status: RazorSearchQueueStatusResponse) => void,
  ) {
    const normalizedEvent = rawEvent.replace(/\r/g, "");
    const lines = normalizedEvent.split("\n");
    let eventName = "message";
    const dataLines: string[] = [];

    for (const line of lines) {
      if (line.startsWith("event:")) {
        eventName = line.slice(6).trim();
        continue;
      }

      if (line.startsWith("data:")) {
        dataLines.push(line.slice(5).trimStart());
      }
    }

    if (eventName !== "queue-status" || dataLines.length === 0) {
      return;
    }

    try {
      const payload = JSON.parse(dataLines.join("\n"));
      const status = normalizeQueueStatusResponse(payload);
      if (status) {
        onStatus(status);
      }
    } catch {
      // Ignore malformed stream frames and wait for the next valid event.
    }
  }
}
