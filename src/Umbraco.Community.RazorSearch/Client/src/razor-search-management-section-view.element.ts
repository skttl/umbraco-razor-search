import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { css, html, nothing } from "@umbraco-cms/backoffice/external/lit";
import {
  RazorSearchManagementClient,
  type QueueRazorSearchPublishedContentResponse,
  type RazorSearchDocumentStatusResponse,
  type RazorSearchQueueStatusResponse,
} from "./razor-search-management.client.js";

type AsyncState = "idle" | "loading";

export class RazorSearchManagementWorkspaceViewElement extends UmbLitElement {
  static properties = {
    _documentId: { state: true },
    _includeDescendants: { state: true },
    _documentStatus: { state: true },
    _documentLookupMessage: { state: true },
    _documentLookupState: { state: true },
    _queueState: { state: true },
    _queueStatus: { state: true },
    _queueStatusState: { state: true },
    _backfillState: { state: true },
    _backfillResult: { state: true },
    _maintenanceMessage: { state: true },
  };

  static styles = css`
    :host {
      display: block;
      padding: var(--uui-size-layout-1);
      color: var(--uui-color-text);
    }

    .layout {
      display: grid;
      gap: var(--uui-size-space-5);
      max-width: 1240px;
    }

    .hero {
      display: grid;
      gap: var(--uui-size-space-3);
      padding: var(--uui-size-space-5);
      border-radius: calc(var(--uui-border-radius) * 1.5);
      background:
        radial-gradient(circle at top right, rgba(61, 179, 113, 0.18), transparent 38%),
        linear-gradient(135deg, rgba(22, 103, 86, 0.08), rgba(7, 52, 74, 0.02));
      border: 1px solid color-mix(in srgb, var(--uui-color-divider-emphasis) 30%, transparent);
    }

    .eyebrow {
      margin: 0;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      font-size: 0.78rem;
      color: var(--uui-color-text-alt);
    }

    .hero h2,
    .hero p,
    .muted,
    .hint-list {
      margin: 0;
    }

    .muted,
    .hint-list {
      color: var(--uui-color-text-alt);
    }

    .grid {
      display: grid;
      gap: var(--uui-size-space-4);
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
    }

    .grid--wide {
      grid-template-columns: minmax(0, 1.35fr) minmax(280px, 0.65fr);
    }

    .stack {
      display: grid;
      gap: var(--uui-size-space-4);
    }

    .status-topline,
    .status-row,
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--uui-size-space-3);
      align-items: center;
    }

    .status-topline {
      justify-content: space-between;
    }

    .status-summary {
      display: grid;
      gap: var(--uui-size-space-2);
    }

    .status-summary strong {
      font-size: 1.1rem;
    }

    .progress-shell {
      display: grid;
      gap: var(--uui-size-space-2);
    }

    .progress-track {
      height: 14px;
      overflow: hidden;
      border-radius: 999px;
      background: color-mix(in srgb, var(--uui-color-surface-alt) 88%, var(--uui-color-divider));
      box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--uui-color-divider) 75%, transparent);
    }

    .progress-value {
      height: 100%;
      border-radius: inherit;
      background: linear-gradient(90deg, #1f8f6a, #44c28e);
      transition: width 180ms ease;
    }

    .progress-value.is-idle {
      background: linear-gradient(90deg, #6b7280, #9ca3af);
    }

    .metrics {
      display: grid;
      gap: var(--uui-size-space-3);
      grid-template-columns: repeat(auto-fit, minmax(130px, 1fr));
    }

    .metric {
      border: 1px solid var(--uui-color-divider);
      border-radius: var(--uui-border-radius);
      padding: var(--uui-size-space-4);
      background: var(--uui-color-surface-alt);
      display: grid;
      gap: var(--uui-size-space-2);
    }

    .metric-label {
      display: block;
      font-size: 0.8rem;
      color: var(--uui-color-text-alt);
    }

    .metric-value {
      display: block;
      font-size: 1.8rem;
      font-weight: 700;
      line-height: 1.1;
    }

    .field-grid {
      display: grid;
      gap: var(--uui-size-space-3);
    }

    .toggle {
      display: inline-flex;
      gap: var(--uui-size-space-2);
      align-items: center;
      font-size: 0.95rem;
    }

    .status-panel {
      border: 1px solid var(--uui-color-divider);
      border-radius: var(--uui-border-radius);
      padding: var(--uui-size-space-4);
      background: var(--uui-color-surface-alt);
      display: grid;
      gap: var(--uui-size-space-3);
    }

    .notice {
      padding: var(--uui-size-space-3);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface-alt);
      border-left: 4px solid var(--uui-color-divider-emphasis);
    }

    .hint-list {
      padding-left: 1.25rem;
      display: grid;
      gap: var(--uui-size-space-2);
    }

    @media (max-width: 900px) {
      .grid--wide {
        grid-template-columns: 1fr;
      }
    }
  `;

  readonly #managementClient = new RazorSearchManagementClient(this);
  #notificationContext?: {
    peek: (...args: any[]) => unknown;
  };
  #queueStatusPollHandle?: number;

  declare _documentId: string;
  declare _includeDescendants: boolean;
  declare _documentStatus: RazorSearchDocumentStatusResponse | undefined;
  declare _documentLookupMessage: string | undefined;
  declare _documentLookupState: AsyncState;
  declare _queueState: AsyncState;
  declare _queueStatus: RazorSearchQueueStatusResponse | undefined;
  declare _queueStatusState: AsyncState;
  declare _backfillState: AsyncState;
  declare _backfillResult: QueueRazorSearchPublishedContentResponse | undefined;
  declare _maintenanceMessage: string | undefined;

  constructor() {
    super();
    this._documentId = "";
    this._includeDescendants = true;
    this._documentStatus = undefined;
    this._documentLookupMessage = undefined;
    this._documentLookupState = "idle";
    this._queueState = "idle";
    this._queueStatus = undefined;
    this._queueStatusState = "idle";
    this._backfillState = "idle";
    this._backfillResult = undefined;
    this._maintenanceMessage = undefined;

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (instance) => {
      this.#notificationContext = instance;
    });
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#refreshQueueStatus(true);
    this.#startQueueStatusPolling();
  }

  override disconnectedCallback() {
    this.#stopQueueStatusPolling();
    super.disconnectedCallback();
  }

  override render() {
    return html`
      <div class="layout">
        <section class="hero">
          <div>
            <p class="eyebrow">Queue monitor</p>
            <h2>RazorSearch operations</h2>
          </div>
          <p>
            Monitor live render queue activity, run site-wide backfills, and inspect
            a single document when you need to requeue or troubleshoot indexing.
          </p>
        </section>

        <section class="grid grid--wide">
          <uui-box headline="Live queue status">
            ${this.#renderQueueStatus()}
          </uui-box>

          <uui-box headline="What you can do here">
            <div class="stack">
              <p class="muted">
                Use this workspace when search snapshots need attention or a batch
                needs to be kicked off manually.
              </p>
              <ul class="hint-list">
                <li>Run a site backfill after enabling RazorSearch or changing extraction rules.</li>
                <li>Check a specific document GUID to see whether its render jobs are queued, completed, or failing.</li>
                <li>Queue a document tree when a local content area needs to be refreshed without rebuilding the whole site.</li>
              </ul>
            </div>
          </uui-box>
        </section>

        <section class="grid">
          <uui-box headline="Site backfill">
            <div class="stack">
              <p class="muted">
                Queue all currently published content for RazorSearch rendering.
                Use this for first-run population and broad maintenance work.
              </p>
              <div class="actions">
                <uui-button
                  color="positive"
                  look="primary"
                  label="Backfill published content"
                  ?disabled=${this._backfillState === "loading"}
                  @click=${this.#backfillPublishedContent}>
                  Backfill published content
                </uui-button>
              </div>
              ${this._maintenanceMessage
                ? html`<div class="notice">${this._maintenanceMessage}</div>`
                : nothing}
              ${this._backfillResult
                ? this.#renderBackfillResult(this._backfillResult)
                : html`
                    <p class="muted">
                      The rebuild call only queues work. Progress appears in the live
                      queue card while the background renderer drains the batch.
                    </p>
                  `}
            </div>
          </uui-box>

          <uui-box headline="Single document">
            <div class="stack">
              <div class="field-grid">
                <uui-input
                  label="Document ID"
                  placeholder="Enter a document GUID"
                  .value=${this._documentId}
                  @input=${this.#onDocumentIdInput}>
                </uui-input>
                <label class="toggle">
                  <input
                    type="checkbox"
                    .checked=${this._includeDescendants}
                    @change=${this.#onIncludeDescendantsChange} />
                  <span>Include descendants when queueing</span>
                </label>
              </div>
              <div class="actions">
                <uui-button
                  look="outline"
                  label="Load status"
                  ?disabled=${this._documentLookupState === "loading"}
                  @click=${this.#lookupDocumentStatus}>
                  Load status
                </uui-button>
                <uui-button
                  color="positive"
                  look="primary"
                  label="Queue document"
                  ?disabled=${this._queueState === "loading"}
                  @click=${this.#queueDocument}>
                  Queue document
                </uui-button>
              </div>
              ${this._documentStatus
                ? this.#renderDocumentStatus(this._documentStatus)
                : this._documentLookupMessage
                  ? html`<div class="notice">${this._documentLookupMessage}</div>`
                  : html`
                      <p class="muted">
                        Enter a document ID to inspect the current RazorSearch job
                        state or queue it manually.
                      </p>
                    `}
            </div>
          </uui-box>
        </section>
      </div>
    `;
  }

  #renderQueueStatus() {
    const status = this._queueStatus;

    if (!status && this._queueStatusState === "loading") {
      return html`
        <div class="stack">
          <p class="muted">Loading current RazorSearch queue activity...</p>
        </div>
      `;
    }

    const total = status?.totalJobCount ?? 0;
    const completed = status?.completedJobCount ?? 0;
    const failed = status?.failedJobCount ?? 0;
    const cancelled = status?.cancelledJobCount ?? 0;
    const finished = completed + failed + cancelled;
    const progressPercent = status?.progressPercent ?? 0;
    const progressLabel =
      total > 0 ? `${progressPercent}% complete` : "Queue idle";
    const progressClass = status?.state === "idle" ? "progress-value is-idle" : "progress-value";

    return html`
      <div class="stack">
        <div class="status-topline">
          <div class="status-summary">
            <strong>${progressLabel}</strong>
            <span class="muted">
              ${status?.message ??
              "Queue a document or run a published content backfill to start work."}
            </span>
          </div>
          <uui-badge color=${this.#badgeColor(status?.state ?? "idle")}>
            ${status?.state ?? "idle"}
          </uui-badge>
        </div>

        <div class="progress-shell">
          <div
            class="progress-track"
            role="progressbar"
            aria-label="RazorSearch queue progress"
            aria-valuemin="0"
            aria-valuemax="100"
            aria-valuenow=${progressPercent}>
            <div class=${progressClass} style="width: ${progressPercent}%;"></div>
          </div>
          <span class="muted">
            ${total > 0
              ? `${finished} of ${total} jobs have finished in the current batch.`
              : "No jobs are currently active in the queue."}
          </span>
        </div>

        <div class="metrics">
          <div class="metric">
            <span class="metric-label">Batch total</span>
            <span class="metric-value">${total}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Queued</span>
            <span class="metric-value">${status?.pendingJobCount ?? 0}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Running</span>
            <span class="metric-value">${status?.runningJobCount ?? 0}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Completed</span>
            <span class="metric-value">${completed}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Failed</span>
            <span class="metric-value">${failed}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Cancelled</span>
            <span class="metric-value">${cancelled}</span>
          </div>
        </div>

        <p class="muted">
          Updated: ${this.#formatDate(status?.updatedAt) ?? "Unknown"}
        </p>
      </div>
    `;
  }

  #renderDocumentStatus(status: RazorSearchDocumentStatusResponse) {
    return html`
      <div class="status-panel">
        <div class="status-row">
          <strong>State:</strong>
          <uui-badge color=${this.#badgeColor(status.state)}>${status.state}</uui-badge>
          ${status.message ? html`<span>${status.message}</span>` : nothing}
        </div>
        <div class="metrics">
          <div class="metric">
            <span class="metric-label">Pending</span>
            <span class="metric-value">${status.pendingDocumentCount}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Completed</span>
            <span class="metric-value">${status.completedDocumentCount}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Failed</span>
            <span class="metric-value">${status.failedDocumentCount}</span>
          </div>
        </div>
        <p class="muted">
          Includes descendants: ${status.includeDescendants ? "Yes" : "No"}
        </p>
        <p class="muted">
          Updated: ${this.#formatDate(status.updatedAt) ?? "Unknown"}
        </p>
      </div>
    `;
  }

  #renderBackfillResult(result: QueueRazorSearchPublishedContentResponse) {
    return html`
      <div class="status-panel">
        <div class="status-row">
          <strong>State:</strong>
          <uui-badge color=${this.#badgeColor(result.state)}>${result.state}</uui-badge>
          <span>${result.message}</span>
        </div>
        <div class="metrics">
          <div class="metric">
            <span class="metric-label">Discovered</span>
            <span class="metric-value">${result.discoveredDocumentCount}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Processed</span>
            <span class="metric-value">${result.processedDocumentCount}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Queued routes</span>
            <span class="metric-value">${result.queuedRouteCount}</span>
          </div>
          <div class="metric">
            <span class="metric-label">Duplicate routes</span>
            <span class="metric-value">${result.duplicateRouteCount}</span>
          </div>
        </div>
        <p class="muted">
          Max documents: ${result.maxDocumentCount}. Skipped documents:
          ${result.skippedDocumentCount}.
        </p>
        <p class="muted">
          Queued: ${this.#formatDate(result.queuedAt) ?? "Unknown"}
        </p>
      </div>
    `;
  }

  async #lookupDocumentStatus() {
    if (!this.#ensureDocumentId()) {
      return;
    }

    this._documentLookupState = "loading";
    this._documentLookupMessage = undefined;
    try {
      const status = await this.#managementClient.getDocumentStatus(
        this._documentId.trim(),
      );

      if (status) {
        this._documentStatus = status;
        return;
      }

      this._documentStatus = undefined;
      this._documentLookupMessage =
        "The document status lookup completed, but no readable status payload was returned.";
    } finally {
      this._documentLookupState = "idle";
    }
  }

  async #queueDocument() {
    if (!this.#ensureDocumentId()) {
      return;
    }

    this._queueState = "loading";
    try {
      const response = await this.#managementClient.queueDocument(
        this._documentId.trim(),
        this._includeDescendants,
      );

      if (!response) {
        return;
      }

      this._documentStatus = response.status;
      this._documentLookupMessage = undefined;
      this.#notify("positive", response.message);
      await this.#refreshQueueStatus();
    } finally {
      this._queueState = "idle";
    }
  }

  async #backfillPublishedContent() {
    this._backfillState = "loading";
    try {
      const response = await this.#managementClient.backfillPublishedContent();
      if (!response) {
        this._maintenanceMessage =
          "The published content rebuild request could not be completed.";
        return;
      }

      this._backfillResult = response;
      this._maintenanceMessage = response.message;
      this.#notify("positive", response.message);
      await this.#refreshQueueStatus();
    } finally {
      this._backfillState = "idle";
    }
  }

  async #refreshQueueStatus(showLoading = false) {
    if (showLoading) {
      this._queueStatusState = "loading";
    }

    try {
      const status = await this.#managementClient.getQueueStatus();
      if (status) {
        this._queueStatus = status;
      }
    } finally {
      this._queueStatusState = "idle";
    }
  }

  #startQueueStatusPolling() {
    this.#stopQueueStatusPolling();
    this.#queueStatusPollHandle = window.setInterval(() => {
      void this.#refreshQueueStatus();
    }, 3000);
  }

  #stopQueueStatusPolling() {
    if (this.#queueStatusPollHandle === undefined) {
      return;
    }

    window.clearInterval(this.#queueStatusPollHandle);
    this.#queueStatusPollHandle = undefined;
  }

  #onDocumentIdInput(event: Event) {
    const target = event.target as HTMLInputElement | null;
    this._documentId = target?.value ?? "";
    this._documentLookupMessage = undefined;
  }

  #onIncludeDescendantsChange(event: Event) {
    const target = event.target as HTMLInputElement | null;
    this._includeDescendants = target?.checked ?? false;
  }

  #ensureDocumentId(): boolean {
    if (this._documentId.trim()) {
      return true;
    }

    this.#notify("warning", "Enter a document ID before running this action.");
    return false;
  }

  #notify(color: string, message: string) {
    this.#notificationContext?.peek(color, {
      data: {
        message,
      },
    });
  }

  #badgeColor(state: string) {
    const normalized = state.toLowerCase();

    if (normalized.includes("fail")) {
      return "danger";
    }

    if (normalized.includes("complete") || normalized.includes("done")) {
      return "positive";
    }

    if (normalized.includes("run")) {
      return "positive";
    }

    if (normalized.includes("queue") || normalized.includes("pending")) {
      return "warning";
    }

    return "default";
  }

  #formatDate(value?: string) {
    if (!value) {
      return undefined;
    }

    const date = new Date(value);
    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(date);
  }
}

customElements.define(
  "razor-search-management-workspace-view",
  RazorSearchManagementWorkspaceViewElement,
);

export default RazorSearchManagementWorkspaceViewElement;
