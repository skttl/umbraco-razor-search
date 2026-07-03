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
      .metric-grid,
      .metric-button,
      .job-list,
      .job-card,
      .job-meta,
      .empty-state,
      .actions {
        display: grid;
        gap: var(--uui-size-space-4);
        min-width: 0;
      }

      .layout {
        padding-bottom: var(--uui-size-space-4);
      }

      .summary-header,
      .summary-copy,
      .summary-meta,
      .job-header,
      .actions {
        display: flex;
        gap: var(--uui-size-space-3);
        align-items: center;
        flex-wrap: wrap;
      }

      .summary-header,
      .job-header {
        justify-content: space-between;
      }

      .summary-copy,
      .summary-meta,
      .job-header {
        min-width: 0;
      }

      .summary-copy h3,
      .job-title {
        margin: 0;
        min-width: 0;
        overflow-wrap: anywhere;
      }

      .summary-meta,
      .muted,
      .metric-label,
      .job-meta p,
      .empty-copy {
        color: var(--uui-color-text-alt);
      }

      .metric-grid {
        grid-template-columns: minmax(0, 1fr);
      }

      .metric-button {
        text-align: left;
        align-content: start;
        padding: var(--uui-size-space-4);
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        cursor: pointer;
        transition:
          border-color 120ms ease,
          background-color 120ms ease;
      }

      .metric-button:hover,
      .metric-button:focus-visible {
        border-color: var(--uui-color-interactive-emphasis);
        background: color-mix(
          in srgb,
          var(--uui-color-interactive-emphasis) 5%,
          var(--uui-color-surface)
        );
      }

      .metric-button.is-active {
        border-color: var(--uui-color-interactive-emphasis);
        background: color-mix(
          in srgb,
          var(--uui-color-interactive-emphasis) 10%,
          var(--uui-color-surface)
        );
      }

      .metric-label {
        font-size: 0.8rem;
      }

      .metric-value {
        font-size: 1.6rem;
        font-weight: 700;
        line-height: 1;
        color: var(--uui-color-text);
      }

      .job-card,
      .empty-state {
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        padding: var(--uui-size-space-4);
      }

      .job-meta p {
        margin: 0;
        min-width: 0;
        overflow-wrap: anywhere;
      }

      .notice {
        padding: var(--uui-size-space-3);
        border-radius: var(--uui-border-radius);
        border: 1px solid color-mix(in srgb, var(--uui-color-danger) 22%, transparent);
        background: color-mix(
          in srgb,
          var(--uui-color-danger-standalone) 6%,
          var(--uui-color-surface)
        );
      }

      .empty-state {
        justify-items: center;
        text-align: center;
      }

      .actions {
        justify-content: flex-end;
      }

      @media (min-width: 720px) {
        .metric-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }

      @media (min-width: 1040px) {
        .metric-grid {
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
          ${this.#renderFilters(details)}
          ${this.#renderJobs(visibleJobs)}
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

  #renderFilters(details: RazorSearchQueueBatchDetailsResponse) {
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

    return html`
      <div class="metric-grid">
        ${metrics.map(
          (metric) => html`
            <button
              type="button"
              class=${`metric-button ${this._activeFilter === metric.filter ? "is-active" : ""}`}
              @click=${() => this.#setFilter(metric.filter)}>
              <span class="metric-label">${metric.label}</span>
              <span class="metric-value">${metric.value}</span>
            </button>
          `,
        )}
      </div>
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
            <article class="job-card">
              <div class="job-list">
                <div class="job-header">
                  <div class="stack">
                    <h4 class="job-title">
                      ${job.documentName?.trim() || job.documentId}
                    </h4>
                    <span class="muted">${job.route}</span>
                  </div>
                  <uui-badge color=${this.#badgeColor(job.state)}>
                    ${job.state}
                  </uui-badge>
                </div>

                <div class="job-meta">
                  <p>
                    Renderer: ${job.renderer}${job.culture
                      ? ` • Culture: ${job.culture}`
                      : ""}${job.segment ? ` • Segment: ${job.segment}` : ""}
                  </p>
                  <p>
                    Enqueued: ${this.#formatDate(job.enqueuedAt) ?? "Unknown"}${job.startedAt
                      ? ` • Started: ${this.#formatDate(job.startedAt)}`
                      : ""}${job.completedAt
                      ? ` • Completed: ${this.#formatDate(job.completedAt)}`
                      : ""}
                  </p>
                  <p>
                    Last update: ${this.#formatDate(job.updatedAt) ?? "Unknown"}
                  </p>
                </div>

                ${job.errorMessage
                  ? html`<div class="notice">${job.errorMessage}</div>`
                  : nothing}
              </div>
            </article>
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
      dateStyle: "medium",
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
