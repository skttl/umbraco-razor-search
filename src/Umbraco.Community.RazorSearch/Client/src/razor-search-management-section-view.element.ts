import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { umbOpenModal } from "@umbraco-cms/backoffice/modal";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { css, html, nothing } from "@umbraco-cms/backoffice/external/lit";
import {
  RazorSearchManagementClient,
  type RazorSearchQueueBatchDetailsResponse,
  type RazorSearchQueueStatusStreamHandle,
  type RazorSearchQueueJobResponse,
  type RazorSearchQueueStatusResponse,
} from "./razor-search-management.client.js";
import {
  RAZOR_SEARCH_QUEUE_DETAILS_MODAL,
  type RazorSearchQueueBatchFilter,
} from "./razor-search-queue-details-modal.token.js";
import { RAZOR_SEARCH_QUEUE_MODAL } from "./razor-search-queue-modal.token.js";
import { RAZOR_SEARCH_STATUS_MODAL } from "./razor-search-status-modal.token.js";

type AsyncState = "idle" | "loading";

export class RazorSearchManagementWorkspaceViewElement extends UmbLitElement {
  static properties = {
    _documentId: { state: true },
    _documentLookupMessage: { state: true },
    _documentLookupState: { state: true },
    _queueState: { state: true },
    _queueStatus: { state: true },
    _queueStatusState: { state: true },
    _queueDetailsState: { state: true },
    _backfillState: { state: true },
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

    .status-badge {
      position: static;
      align-self: flex-start;
      flex-shrink: 0;
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

    .metrics--queue {
      gap: var(--uui-size-space-2);
      grid-template-columns: repeat(6, minmax(0, 1fr));
    }

    .metric {
      border: 1px solid var(--uui-color-divider);
      border-radius: var(--uui-border-radius);
      padding: var(--uui-size-space-4);
      background: var(--uui-color-surface-alt);
      display: grid;
      gap: var(--uui-size-space-2);
      min-width: 0;
    }

    .metrics--queue .metric {
      padding: var(--uui-size-space-3);
    }

    .metric-button {
      width: 100%;
      border: 0;
      padding: 0;
      margin: 0;
      background: transparent;
      color: inherit;
      font: inherit;
      text-align: left;
      cursor: pointer;
      display: grid;
      gap: var(--uui-size-space-2);
    }

    .metric-button:disabled {
      cursor: default;
      opacity: 0.65;
    }

    .metric-button:not(:disabled):hover .metric-label,
    .metric-button:not(:disabled):focus-visible .metric-label {
      color: var(--uui-color-interactive-emphasis);
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

    .field-label {
      font-size: 0.95rem;
      font-weight: 600;
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

    @media (max-width: 720px) {
      .metrics--queue {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }
    }

    @media (max-width: 520px) {
      .metrics--queue {
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }
    }
  `;

  readonly #managementClient = new RazorSearchManagementClient(this);
  #notificationContext?: {
    peek: (...args: any[]) => unknown;
  };
  #queueStatusStream?: RazorSearchQueueStatusStreamHandle;
  #queueStatusReconnectHandle?: number;

  declare _documentId: string;
  declare _documentLookupMessage: string | undefined;
  declare _documentLookupState: AsyncState;
  declare _queueState: AsyncState;
  declare _queueStatus: RazorSearchQueueStatusResponse | undefined;
  declare _queueStatusState: AsyncState;
  declare _queueDetailsState: AsyncState;
  declare _backfillState: AsyncState;
  declare _maintenanceMessage: string | undefined;

  constructor() {
    super();
    this._documentId = "";
    this._documentLookupMessage = undefined;
    this._documentLookupState = "idle";
    this._queueState = "idle";
    this._queueStatus = undefined;
    this._queueStatusState = "idle";
    this._queueDetailsState = "idle";
    this._backfillState = "idle";
    this._maintenanceMessage = undefined;

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (instance) => {
      this.#notificationContext = instance;
    });
  }

  override connectedCallback() {
    super.connectedCallback();
    void this.#refreshQueueStatus(true);
    void this.#startQueueStatusFeed();
  }

  override disconnectedCallback() {
    this.#stopQueueStatusStream();
    super.disconnectedCallback();
  }

  override render() {
    return html`
      <div class="layout">
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
              <p class="muted">
                The backfill action only queues work. Progress appears in the live
                queue card while the background renderer drains the batch.
              </p>
            </div>
          </uui-box>

          <uui-box headline="Single document">
            <div class="stack">
              <div class="field-grid">
                <span class="field-label">Document</span>
                <umb-input-document
                  max="1"
                  .selection=${this._documentId ? [this._documentId] : []}
                  @change=${this.#onDocumentSelectionChange}>
                </umb-input-document>
              </div>
              <div class="actions">
                <uui-button
                  look="outline"
                  label="Load status"
                  ?disabled=${this._documentLookupState === "loading" || !this.#hasDocumentId()}
                  @click=${this.#lookupDocumentStatus}>
                  Load status
                </uui-button>
                <uui-button
                  color="positive"
                  look="primary"
                  label="Queue document"
                  ?disabled=${this._queueState === "loading" || !this.#hasDocumentId()}
                  @click=${this.#queueDocument}>
                  Queue document
                </uui-button>
              </div>
              ${this._documentLookupMessage
                ? html`<div class="notice">${this._documentLookupMessage}</div>`
                : html`
                    <p class="muted">
                      Pick a document to inspect the current RazorSearch job
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
          <uui-badge class="status-badge" color=${this.#badgeColor(status?.state ?? "idle")}>
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

        <div class="metrics metrics--queue">
          <div class="metric">
            ${this.#renderQueueMetric("Batch total", total, "all")}
          </div>
          <div class="metric">
            ${this.#renderQueueMetric("Queued", status?.pendingJobCount ?? 0, "queued")}
          </div>
          <div class="metric">
            ${this.#renderQueueMetric("Running", status?.runningJobCount ?? 0, "running")}
          </div>
          <div class="metric">
            ${this.#renderQueueMetric("Completed", completed, "completed")}
          </div>
          <div class="metric">
            ${this.#renderQueueMetric("Failed", failed, "failed")}
          </div>
          <div class="metric">
            ${this.#renderQueueMetric("Cancelled", cancelled, "cancelled")}
          </div>
        </div>

        ${status?.currentJob
          ? this.#renderCurrentJob(status.currentJob)
          : nothing}

        <p class="muted">
          Updated: ${this.#formatDate(status?.updatedAt) ?? "Unknown"}
        </p>
      </div>
    `;
  }

  #renderQueueMetric(
    label: string,
    value: number,
    filter: RazorSearchQueueBatchFilter,
  ) {
    return html`
      <button
        type="button"
        class="metric-button"
        ?disabled=${this._queueDetailsState === "loading"}
        @click=${() => this.#openQueueBatchDetails(filter)}>
        <span class="metric-label">${label}</span>
        <span class="metric-value">${value}</span>
      </button>
    `;
  }

  #renderCurrentJob(job: RazorSearchQueueJobResponse) {
    const label = job.state === "running" ? "Currently rendering" : "Next in queue";
    const title = job.documentName?.trim() || job.documentId;

    return html`
      <div class="status-panel">
        <div class="status-row">
          <strong>${label}</strong>
          <uui-badge class="status-badge" color=${this.#badgeColor(job.state)}>
            ${job.state}
          </uui-badge>
        </div>
        <div class="stack">
          <strong>${title}</strong>
          <span class="muted">${job.route}</span>
          <span class="muted">
            Renderer: ${job.renderer}${job.culture ? ` • Culture: ${job.culture}` : ""}
          </span>
          <span class="muted">
            Last update: ${this.#formatDate(job.updatedAt) ?? "Unknown"}
          </span>
          ${job.errorMessage ? html`<div class="notice">${job.errorMessage}</div>` : nothing}
        </div>
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
        await umbOpenModal(this, RAZOR_SEARCH_STATUS_MODAL, {
          data: {
            headline: `RazorSearch status${status.documentName ? `: ${status.documentName}` : ""}`,
            status,
          },
        }).catch(() => undefined);
        return;
      }

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

    const modalValue = await umbOpenModal(this, RAZOR_SEARCH_QUEUE_MODAL, {
      data: {
        headline: "Queue RazorSearch job",
      },
    }).catch(() => undefined);

    if (!modalValue || modalValue.action === "cancel") {
      return;
    }

    this._queueState = "loading";
    try {
      const response = await this.#managementClient.queueDocument(
        this._documentId.trim(),
        modalValue.action === "descendants",
      );

      if (!response) {
        return;
      }

      this._documentLookupMessage = undefined;
      this.#notify("positive", response.message);
      await this.#refreshQueueStatus();
    } finally {
      this._queueState = "idle";
    }
  }

  async #backfillPublishedContent() {
    this._backfillState = "loading";
    this._maintenanceMessage = undefined;
    try {
      const response = await this.#managementClient.backfillPublishedContent();
      if (!response) {
        this._maintenanceMessage =
          "The published content rebuild request could not be completed.";
        return;
      }

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

  async #openQueueBatchDetails(filter: RazorSearchQueueBatchFilter) {
    this._queueDetailsState = "loading";

    try {
      const details = await this.#managementClient.getQueueBatchDetails();
      if (!details) {
        this.#notify(
          "warning",
          "The queue batch details could not be loaded right now.",
        );
        return;
      }

      await umbOpenModal(this, RAZOR_SEARCH_QUEUE_DETAILS_MODAL, {
        data: {
          headline: this.#queueBatchHeadline(details),
          filter,
          details,
        },
      }).catch(() => undefined);
    } finally {
      this._queueDetailsState = "idle";
    }
  }

  async #startQueueStatusFeed() {
    this.#stopQueueStatusStream();
    this.#clearQueueStatusReconnect();

    const stream = await this.#managementClient.createQueueStatusStream(
      (status) => {
        this._queueStatus = status;
        this._queueStatusState = "idle";
      },
      () => {
        this.#stopQueueStatusStream();
        this.#scheduleQueueStatusReconnect();
      },
    );

    if (!stream) {
      this.#scheduleQueueStatusReconnect();
      return;
    }

    this.#queueStatusStream = stream;
  }

  #stopQueueStatusStream() {
    this.#clearQueueStatusReconnect();
    this.#queueStatusStream?.close();
    this.#queueStatusStream = undefined;
  }

  #scheduleQueueStatusReconnect() {
    if (this.#queueStatusReconnectHandle !== undefined || !this.isConnected) {
      return;
    }

    this.#queueStatusReconnectHandle = window.setTimeout(() => {
      this.#queueStatusReconnectHandle = undefined;
      void this.#startQueueStatusFeed();
    }, 3000);
  }

  #clearQueueStatusReconnect() {
    if (this.#queueStatusReconnectHandle === undefined) {
      return;
    }

    window.clearTimeout(this.#queueStatusReconnectHandle);
    this.#queueStatusReconnectHandle = undefined;
  }

  #onDocumentSelectionChange(event: Event) {
    const target = event.target as (EventTarget & { selection?: string[] }) | null;
    this._documentId = target?.selection?.[0] ?? "";
    this._documentLookupMessage = undefined;
  }

  #hasDocumentId() {
    return !!this._documentId.trim();
  }

  #ensureDocumentId(): boolean {
    if (this.#hasDocumentId()) {
      return true;
    }

    this.#notify("warning", "Pick a document before running this action.");
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

  #queueBatchHeadline(details: RazorSearchQueueBatchDetailsResponse) {
    if (details.totalJobCount === 0) {
      return "RazorSearch queue items";
    }

    return details.state === "idle"
      ? "Last RazorSearch batch"
      : "Current RazorSearch batch";
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
