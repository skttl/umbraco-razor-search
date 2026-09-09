import {
  css,
  customElement,
  html,
  nothing,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type {
  UmbModalContext,
  UmbModalExtensionElement,
} from "@umbraco-cms/backoffice/modal";
import type {
  RazorSearchQueueBatchDetailsResponse,
  RazorSearchQueueJobResponse,
} from "./razor-search-management.client.js";
import type {
  RazorSearchQueueBatchFilter,
  RazorSearchQueueDetailsModalData,
  RazorSearchQueueDetailsModalValue,
} from "./razor-search-queue-details-modal.token.js";

@customElement("razor-search-queue-details-modal")
export class RazorSearchQueueDetailsModalElement
  extends UmbLitElement
  implements
    UmbModalExtensionElement<
      RazorSearchQueueDetailsModalData,
      RazorSearchQueueDetailsModalValue
    >
{
  declare modalContext: UmbModalContext<
    RazorSearchQueueDetailsModalData,
    RazorSearchQueueDetailsModalValue
  > | undefined;

  static override properties = {
    modalContext: { attribute: false },
    _activeFilter: { state: true },
  };

  static override styles = [
    css`
      :host {
        display: block;
        height: 100%;
        min-width: 0;
        overflow-x: hidden;
      }

      umb-body-layout {
        min-width: 0;
        --umb-body-layout-main-content-padding: var(--uui-size-space-5);
      }

      uui-box {
        min-width: 0;
      }

      .layout > * {
        min-width: 0;
      }

      .layout,
      .stack,
      .job-list,
      .job-meta-grid,
      .empty-state,
      .actions {
        display: grid;
        gap: var(--uui-size-space-3);
        min-width: 0;
      }

      .layout {
        padding-bottom: var(--uui-size-space-4);
      }

      .summary-header,
      .summary-copy,
      .summary-meta,
      .actions {
        display: flex;
        gap: var(--uui-size-space-3);
        align-items: center;
        flex-wrap: wrap;
      }

      .summary-header {
        justify-content: space-between;
      }

      .summary-copy,
      .summary-meta {
        min-width: 0;
      }

      .summary-copy uui-badge,
      .job-header uui-badge {
        --uui-badge-position: static;
        position: static;
        flex: 0 1 auto;
        min-width: 0;
        max-width: 100%;
        overflow-wrap: anywhere;
        white-space: normal;
      }

      .summary-copy h3 {
        margin: 0;
        min-width: 0;
        overflow-wrap: anywhere;
      }

      .summary-meta,
      .muted,
      .metric-label,
      .empty-copy {
        color: var(--uui-color-text-alt);
      }

      .stack p {
        margin: 0;
      }

      .filter-tabs {
        margin: calc(var(--uui-size-space-2) * -1)
          calc(var(--uui-size-space-2) * -1) 0;
      }

      .filter-tab-content {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
      }

      .filter-tab-count {
        color: var(--uui-color-text-alt);
        font-size: 0.8rem;
      }

      .empty-state {
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        padding: var(--uui-size-space-4);
        max-width: 100%;
        overflow: hidden;
      }

      .job-list {
        gap: var(--uui-size-space-3);
      }

      .batch-meta {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-5);
        padding: var(--uui-size-space-3) 0;
        border-bottom: 1px solid var(--uui-color-divider);
      }

      .batch-meta-item {
        display: grid;
        gap: var(--uui-size-space-1);
      }

      .jobs-heading h4 {
        margin: var(--uui-size-space-3) 0 0;
      }

      .job-card {
        --uui-box-default-padding: var(--uui-size-space-4);
      }

      .job-header,
      .job-heading,
      .job-meta-item {
        display: block;
        min-width: 0;
        overflow-wrap: anywhere;
      }

      .job-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: var(--uui-size-space-3);
      }

      .job-heading {
        display: grid;
        gap: var(--uui-size-space-1);
      }

      .job-name {
        font-weight: 700;
      }

      .job-route,
      .job-secondary {
        color: var(--uui-color-text-alt);
        font-size: 0.8rem;
        line-height: 1.35;
      }

      .job-meta-grid {
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: var(--uui-size-space-3);
      }

      .job-meta-item {
        display: grid;
        gap: var(--uui-size-space-1);
      }

      .job-meta-label {
        color: var(--uui-color-text-alt);
        font-size: 0.75rem;
        font-weight: 700;
        text-transform: uppercase;
        letter-spacing: 0.03em;
      }

      .job-error {
        color: var(--uui-color-danger);
        margin-top: var(--uui-size-space-2);
      }

      .empty-state {
        justify-items: center;
        text-align: center;
      }

      .empty-state uui-symbol-expand-less {
        color: var(--uui-color-text-alt);
        font-size: 1.5rem;
      }

      .actions {
        justify-content: flex-end;
      }

      @media (min-width: 1040px) {
        .job-meta-grid {
          grid-template-columns: repeat(3, minmax(0, 1fr));
        }
      }
    `,
  ];

  declare _activeFilter: RazorSearchQueueBatchFilter;

  constructor() {
    super();
    this._activeFilter = "all";
  }

  override willUpdate(changedProperties: Map<PropertyKey, unknown>) {
    super.willUpdate(changedProperties);

    if (
      changedProperties.has("modalContext") &&
      this.modalContext?.data.filter
    ) {
      this._activeFilter = this.modalContext.data.filter;
    }
  }

  #submit() {
    this.modalContext?.updateValue({ action: "close" });
    this.modalContext?.submit();
  }

  #setFilter(filter: RazorSearchQueueBatchFilter) {
    this._activeFilter = filter;
  }

  override render() {
    const headline =
      this.modalContext?.data.headline ?? "RazorSearch queue items";
    const details = this.modalContext?.data.details;

    if (!details) {
      return html`
        <umb-body-layout .headline=${headline}>
          <div class="layout">
            ${this.#renderEmptyState(
              "No queue batch details were supplied for this dialog.",
            )}
          </div>
          <div slot="actions" class="actions">
            <uui-button
              look="primary"
              label="Close"
              @click=${() => this.#submit()}></uui-button>
          </div>
        </umb-body-layout>
      `;
    }

    const visibleJobs = this.#getVisibleJobs(details);

    return html`
      <umb-body-layout .headline=${headline}>
        <div class="layout">
          ${this.#renderSummary(details)}
          ${this.#renderBatchOverview(details, visibleJobs, details.jobs)}
        </div>
        <div slot="actions" class="actions">
          <uui-button
            look="primary"
            label="Close"
            @click=${() => this.#submit()}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  #renderSummary(details: RazorSearchQueueBatchDetailsResponse) {
    return html`
      <uui-box>
        <div slot="headline" class="summary-header">
          <div class="summary-copy">
            <h3>${this.#summaryTitle(details)}</h3>
            <uui-badge color=${this.#badgeColor(details.state)}>
              ${details.state}
            </uui-badge>
          </div>
          <div class="summary-meta">
            Updated: ${this.#formatDate(details.updatedAt) ?? "Unknown"}
          </div>
        </div>

        <div class="stack">
          ${details.message ? html`<p class="muted">${details.message}</p>` : nothing}
          <p class="muted">
            ${details.totalJobCount > 0
              ? `${details.progressPercent}% of this batch is complete.`
              : "No jobs are available in the selected batch yet."}
          </p>
        </div>
      </uui-box>
    `;
  }

  #renderBatchOverview(
    details: RazorSearchQueueBatchDetailsResponse,
    jobs: RazorSearchQueueJobResponse[],
    allJobs: RazorSearchQueueJobResponse[],
  ) {
    const metrics: Array<{
      filter: RazorSearchQueueBatchFilter;
      label: string;
      value: number;
    }> = [
      { filter: "all", label: "Batch total", value: details.totalJobCount },
      { filter: "queued", label: "Queued", value: details.pendingJobCount },
      { filter: "running", label: "Running", value: details.runningJobCount },
      {
        filter: "completed",
        label: "Completed",
        value: details.completedJobCount,
      },
      { filter: "failed", label: "Failed", value: details.failedJobCount },
      {
        filter: "cancelled",
        label: "Cancelled",
        value: details.cancelledJobCount,
      },
    ];

    const renderers = [...new Set(allJobs.map((job) => job.renderer))];
    const cultures = [...new Set(allJobs.map((job) => job.culture).filter(Boolean))];

    return html`
      <uui-box headline="Batch overview">
        <uui-tab-group class="filter-tabs" aria-label="Filter queue jobs">
          ${metrics.map(
            (metric) => html`
              <uui-tab
                ?active=${this._activeFilter === metric.filter}
                @click=${() => this.#setFilter(metric.filter)}>
                <span class="filter-tab-content">
                  ${metric.label}
                  <span class="filter-tab-count">${metric.value}</span>
                </span>
              </uui-tab>
            `,
          )}
        </uui-tab-group>

        <div class="batch-meta">
          <div class="batch-meta-item">
            <span class="job-meta-label">Renderer</span>
            <span>${renderers.length > 0 ? renderers.join(", ") : "Unknown"}</span>
          </div>
          ${cultures.length > 0
            ? html`
                <div class="batch-meta-item">
                  <span class="job-meta-label">Cultures</span>
                  <span>${cultures.join(", ")}</span>
                </div>
              `
            : nothing}
        </div>

        <div class="jobs-heading">
          <h4>Jobs (${jobs.length})</h4>
        </div>
        ${this.#renderJobs(jobs)}
      </uui-box>
    `;
  }

  #renderJobs(jobs: RazorSearchQueueJobResponse[]) {
    if (jobs.length === 0) {
      return this.#renderEmptyState(this.#emptyFilterMessage());
    }

    return html`
      <div class="job-list">
          ${jobs.map(
            (job) => html`
              <uui-box class="job-card">
                <div slot="headline" class="job-header">
                  <div class="job-heading">
                    <span class="job-name">
                      ${job.documentName?.trim() || job.documentId}
                    </span>
                    <span class="job-route">${job.route}</span>
                  </div>
                  <uui-badge color=${this.#badgeColor(job.state)}>
                    ${job.state}
                  </uui-badge>
                </div>

                <div class="job-meta-grid">
                  <div class="job-meta-item">
                    <span class="job-meta-label">Enqueued</span>
                    <span>${this.#formatDate(job.enqueuedAt) ?? "Unknown"}</span>
                  </div>
                  <div class="job-meta-item">
                    <span class="job-meta-label">Started / completed</span>
                    <span>
                      ${job.startedAt ? this.#formatDate(job.startedAt) : "Not started"}
                      ${job.completedAt
                        ? ` · ${this.#formatDate(job.completedAt)}`
                        : ""}
                    </span>
                  </div>
                  <div class="job-meta-item">
                    <span class="job-meta-label">Last update</span>
                    <span>${this.#formatDate(job.updatedAt) ?? "Unknown"}</span>
                  </div>
                </div>

                ${job.errorMessage
                  ? html`<div class="job-error">${job.errorMessage}</div>`
                  : nothing}
              </uui-box>
            `,
          )}
      </div>
    `;
  }

  #getVisibleJobs(details: RazorSearchQueueBatchDetailsResponse) {
    if (this._activeFilter === "all") {
      return details.jobs;
    }

    return details.jobs.filter((job) => this.#matchesFilter(job, this._activeFilter));
  }

  #matchesFilter(
    job: RazorSearchQueueJobResponse,
    filter: RazorSearchQueueBatchFilter,
  ) {
    switch (filter) {
      case "queued":
        return job.state === "queued";
      case "running":
        return job.state === "running";
      case "completed":
        return (
          job.state === "completed" ||
          job.state === "succeeded" ||
          job.state === "success"
        );
      case "failed":
        return job.state === "failed";
      case "cancelled":
        return job.state === "cancelled";
      case "all":
      default:
        return true;
    }
  }

  #summaryTitle(details: RazorSearchQueueBatchDetailsResponse) {
    if (details.totalJobCount === 0) {
      return "No queue items available";
    }

    if (details.state === "idle") {
      return "Last RazorSearch batch";
    }

    return "Active RazorSearch batch";
  }

  #emptyFilterMessage() {
    switch (this._activeFilter) {
      case "queued":
        return "No queued jobs were found in this batch.";
      case "running":
        return "No running jobs were found in this batch.";
      case "completed":
        return "No completed jobs were found in this batch.";
      case "failed":
        return "No failed jobs were found in this batch.";
      case "cancelled":
        return "No cancelled jobs were found in this batch.";
      case "all":
      default:
        return "No queue items are available for this batch.";
    }
  }

  #renderEmptyState(message: string) {
    return html`
      <div class="empty-state">
        <uui-symbol-expand-less></uui-symbol-expand-less>
        <p class="empty-copy">${message}</p>
      </div>
    `;
  }

  #badgeColor(state: string) {
    const normalized = state.toLowerCase();

    if (normalized.includes("fail")) {
      return "danger";
    }

    if (
      normalized.includes("complete") ||
      normalized.includes("done") ||
      normalized.includes("success")
    ) {
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
      dateStyle: "short",
      timeStyle: "short",
    }).format(date);
  }
}

export default RazorSearchQueueDetailsModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "razor-search-queue-details-modal": RazorSearchQueueDetailsModalElement;
  }
}
