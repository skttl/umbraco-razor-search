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
  RazorSearchDocumentSnapshotResponse,
  RazorSearchDocumentStatusResponse,
  RazorSearchQueueJobResponse,
} from "./razor-search-management.client.js";
import type {
  RazorSearchStatusModalData,
  RazorSearchStatusModalValue,
} from "./razor-search-status-modal.token.js";

type StatusTab = "snapshot" | "jobs";

@customElement("razor-search-status-modal")
export class RazorSearchStatusModalElement
  extends UmbLitElement
  implements
    UmbModalExtensionElement<
      RazorSearchStatusModalData,
      RazorSearchStatusModalValue
    >
{
  declare modalContext: UmbModalContext<
    RazorSearchStatusModalData,
    RazorSearchStatusModalValue
  > | undefined;

  static override properties = {
    modalContext: { attribute: false },
    _activeTab: { state: true },
  };

  static override styles = [
    css`
      :host {
        display: block;
        box-sizing: border-box;
        height: 100%;
        max-width: 100%;
        min-width: 0;
        width: 100%;
        overflow-x: clip;
      }

      umb-body-layout {
        box-sizing: border-box;
        width: 100%;
        max-width: 100%;
        min-width: 0;
        overflow-x: clip;
        --umb-body-layout-main-content-padding: var(--uui-size-space-5);
      }

      uui-box {
        box-sizing: border-box;
        width: 100%;
        max-width: 100%;
        min-width: 0;
        overflow-x: hidden;
      }

      .layout,
      .layout > *,
      .content-stack,
      .variant-card {
        box-sizing: border-box;
        max-width: 100%;
        min-width: 0;
      }

      .layout > * {
        min-width: 0;
      }

      .layout {
        width: 100%;
        overflow-x: clip;
        padding: 0;
      }

      .tab-panel,
      .content-stack,
      .variant-card {
        overflow-x: hidden;
      }

      .layout,
      .stack,
      .metric-grid,
      .metric-card,
      .job-list,
      .job-card,
      .tab-layout,
      .content-stack,
      .meta-grid,
      .empty-state {
        display: grid;
        gap: var(--uui-size-space-4);
        min-width: 0;
      }

      .layout {
        padding-bottom: var(--uui-size-space-4);
      }

      .layout > .tab-layout {
        margin-top: 0;
      }

      .job-header,
      .variant-header,
      .tab-header,
      .actions {
        display: flex;
        gap: var(--uui-size-space-3);
        align-items: center;
        flex-wrap: wrap;
        min-width: 0;
      }

      .job-header,
      .variant-header,
      .tab-header {
        min-width: 0;
      }

      .section-title,
      .tab-title,
      .meta-title,
      .job-header strong,
      .variant-header strong {
        min-width: 0;
        overflow-wrap: anywhere;
      }

      .section-title {
        font-size: 1rem;
      }

      .meta,
      .muted,
      .field-hint,
      .empty-copy {
        color: var(--uui-color-text-alt);
      }

      .job-card,
      .variant-card {
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        padding: var(--uui-size-space-4);
        min-width: 0;
        max-width: 100%;
        overflow: hidden;
      }

      .job-header {
        width: 100%;
        min-width: 0;
      }

      .job-header strong {
        flex: 1 1 auto;
        min-width: 0;
      }

      .job-header uui-badge {
        --uui-badge-position: static;
        flex: 0 1 auto;
        min-width: 0;
        max-width: 100%;
        overflow-wrap: anywhere;
        white-space: normal;
      }

      .variant-header uui-badge {
        --uui-badge-position: static;
      }

      .variant-card {
        display: grid;
        gap: var(--uui-size-space-4);
      }

      .snapshot-box {
        min-width: 0;
        max-width: 100%;
        overflow: hidden;
      }

      .variant-meta {
        display: grid;
        gap: var(--uui-size-space-2);
        min-width: 0;
      }

      .tab-layout {
        min-width: 0;
        overflow-x: clip;
      }

      .tab-strip {
        position: sticky;
        top: 0;
        z-index: 2;
        box-sizing: border-box;
        min-width: 0;
        width: 100%;
        margin-left: 0;
        padding-inline: var(--uui-size-layout-1);
        padding-bottom: var(--uui-size-space-2);
        background: var(--uui-color-surface);
        border-bottom: 1px solid var(--uui-color-divider);
        overflow-x: hidden;
        overflow-y: hidden;
      }

      .tab-strip::-webkit-scrollbar {
        height: 0;
      }

      .tab-strip uui-tab-group {
        display: flex;
        width: 100%;
        max-width: 100%;
        min-width: 0;
        flex-wrap: wrap;
      }

      .tab-panel {
        min-width: 0;
        padding-inline: var(--uui-size-layout-1);
        padding-top: var(--uui-size-space-5);
        overflow-x: clip;
      }

      .property-list {
        display: grid;
        gap: 0;
        min-width: 0;
        overflow-x: clip;
      }

      .property-row {
        display: grid;
        grid-template-columns: minmax(11rem, 0.32fr) minmax(0, 1fr);
        column-gap: var(--uui-size-space-4);
        align-items: start;
        min-width: 0;
        padding: var(--uui-size-space-2) 0;
      }

      .property-label {
        min-width: 0;
        color: var(--uui-color-text);
        font-weight: 700;
        overflow-wrap: anywhere;
      }

      .property-editor {
        box-sizing: border-box;
        width: 100%;
        min-width: 0;
        overflow: hidden;
      }

      .property-value {
        display: block;
        margin: 0;
        min-width: 0;
        overflow-wrap: anywhere;
        word-break: break-word;
        line-height: 1.35;
        white-space: pre-wrap;
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







      pre {
        max-height: 16rem;
        max-width: 100%;
        margin: 0;
        overflow-x: hidden;
        overflow-y: auto;
        overflow-wrap: anywhere;
        white-space: pre-wrap;
        word-break: break-word;
        color: var(--uui-color-text-alt);
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        padding: var(--uui-size-space-2);
      }


      p {
        margin: 0;
        min-width: 0;
      }

      .empty-state {
        padding: var(--uui-size-space-6) var(--uui-size-space-4);
        justify-items: center;
        text-align: center;
        border: 1px dashed var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      .actions {
        box-sizing: border-box;
        width: 100%;
        max-width: 100%;
        justify-content: flex-end;
        position: sticky;
        bottom: 0;
        z-index: 1;
        padding: var(--uui-size-space-3) 0;
        background: var(--uui-color-surface);
      }

      .actions uui-button:first-child {
        margin-right: auto;
      }

      .secondary-action {
        justify-self: start;
      }

      @media (min-width: 720px) {
      }

      @media (max-width: 560px) {
        .property-row {
          grid-template-columns: minmax(0, 1fr);
          row-gap: var(--uui-size-space-1);
        }
      }
    `,
  ];

  declare _activeTab: StatusTab;

  constructor() {
    super();
    this._activeTab = "snapshot";
  }

  #submit(action: RazorSearchStatusModalValue["action"]) {
    this.modalContext?.updateValue({ action });
    this.modalContext?.submit();
  }

  #setActiveTab(tab: StatusTab) {
    this._activeTab = tab;
  }

  #onTabChange(event: Event) {
    const tabElement = event
      .composedPath()
      .find(
        (target): target is HTMLElement =>
          target instanceof HTMLElement && target.hasAttribute("data-tab"),
      );

    const tab = tabElement?.getAttribute("data-tab");
    if (
      tab === "snapshot" ||
      tab === "jobs"
    ) {
      this.#setActiveTab(tab);
    }
  }

  override render() {
    const headline =
      this.modalContext?.data.headline ?? "RazorSearch document status";
    const status = this.modalContext?.data.status;

    if (!status) {
      return html`
        <umb-body-layout main-no-padding .headline=${headline}>
          <div class="layout">
            ${this.#renderEmptyState(
              "No status payload was supplied for this dialog.",
            )}
          </div>
          <div slot="actions" class="actions">
            <uui-button
              look="primary"
              label="Close"
              @click=${() => this.#submit("close")}></uui-button>
          </div>
        </umb-body-layout>
      `;
    }

    return html`
      <umb-body-layout main-no-padding .headline=${headline}>
        <div class="layout">
          ${this.#renderTabs(status)}
        </div>

        <div slot="actions" class="actions">
          <uui-button
            look="secondary"
            label="Queue"
            @click=${() => this.#submit("queue")}>Queue</uui-button>
          <uui-button
            look="primary"
            label="Close"
            @click=${() => this.#submit("close")}>Close</uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  #renderJobs(jobs: RazorSearchQueueJobResponse[]) {
    return html`
      <uui-box>
        <h4 slot="headline" class="section-title">Queued Jobs</h4>

        ${jobs.length === 0
          ? this.#renderEmptyState(
              "No queued or historical in-memory jobs are available for this document.",
            )
          : html`
              <div class="job-list">
                ${jobs.map(
                  (job) => html`
                    <article class="job-card">
                      <div class="job-list">
                        <div class="job-header">
                          <strong>${job.route}</strong>
                          <uui-badge color=${this.#badgeColor(job.state)}>
                            ${job.state}
                          </uui-badge>
                        </div>

                        <p class="meta">
                          Renderer: ${job.renderer}${job.culture
                            ? ` • Culture: ${job.culture}`
                            : ""}
                        </p>

                        <p class="meta">
                          Enqueued:
                          ${this.#formatDate(job.enqueuedAt) ?? "Unknown"}${job.startedAt
                            ? ` • Started: ${this.#formatDate(job.startedAt)}`
                            : ""}${job.completedAt
                            ? ` • Completed: ${this.#formatDate(job.completedAt)}`
                            : ""}
                        </p>

                        ${job.errorMessage
                          ? html`<div class="notice"><p>${job.errorMessage}</p></div>`
                          : nothing}
                      </div>
                    </article>
                  `,
                )}

              </div>
            `}
      </uui-box>
    `;
  }

  #renderTabs(status: RazorSearchDocumentStatusResponse) {
    return html`
      <div class="tab-layout">
        <div class="tab-strip">
          <uui-tab-group
            role="tablist"
            aria-label="RazorSearch status tabs"
            @click=${this.#onTabChange}>
            ${this.#renderTab("snapshot", "Snapshot")}
            ${this.#renderTab("jobs", "Jobs Queue")}
          </uui-tab-group>
        </div>

        <div class="tab-panel">${this.#renderActiveTab(status)}</div>
      </div>
    `;
  }

  #renderTab(tab: StatusTab, label: string) {
    return html`
      <uui-tab
        data-tab=${tab}
        ?active=${this._activeTab === tab}
        aria-selected=${this._activeTab === tab ? "true" : "false"}>
        ${label}
      </uui-tab>
    `;
  }

  #renderActiveTab(status: RazorSearchDocumentStatusResponse) {
    switch (this._activeTab) {
      case "snapshot":
        return this.#renderSnapshotTab(status.snapshots);
      case "jobs":
        return this.#renderJobs(status.jobs);
      default:
        return nothing;
    }
  }

  #renderSnapshotTab(snapshots: RazorSearchDocumentSnapshotResponse[]) {
    if (snapshots.length === 0) {
      return this.#renderEmptyState(
        "No stored snapshots were found for this document.",
      );
    }

    return html`
      <div class="content-stack">
        <p class="muted">
          Stored snapshot data after RazorSearch extraction. This is the source
          used to build the indexed fields.
        </p>
        ${snapshots.map(
          (snapshot) => html`
            <uui-box class="snapshot-box">
              <div slot="headline" class="variant-header">
                <strong>${snapshot.culture || "Invariant"}</strong>
                <uui-badge color=${this.#badgeColor(snapshot.state)}>
                  ${snapshot.state}
                </uui-badge>
              </div>

              <div class="property-list">
                ${this.#renderTextCard("Title text", snapshot.titleText)}
                ${this.#renderTextCard("Summary text", snapshot.summaryText)}
                ${this.#renderTextCard("Heading text", snapshot.headingText)}
                ${this.#renderTextCard("Body text", snapshot.bodyText)}
              </div>

              <div class="property-list">
                ${this.#renderMetaCard("URL", snapshot.route)}
                ${this.#renderMetaCard("Final URL", snapshot.finalUrl)}
                ${this.#renderMetaCard("Renderer", snapshot.renderer)}
                ${this.#renderMetaCard("Last render attempt", this.#formatDate(snapshot.lastAttemptAt))}
                ${this.#renderMetaCard("Last successful render", this.#formatDate(snapshot.renderedAt))}
                ${this.#renderMetaCard("Update date", this.#formatDate(snapshot.updatedAt))}
              </div>

              ${snapshot.snapshotHtml !== undefined
                ? this.#renderMetaRow("Full snapshot HTML", snapshot.snapshotHtml)
                : nothing}

              ${snapshot.errorMessage
                ? html`<div class="notice"><p>${snapshot.errorMessage}</p></div>`
                : nothing}
            </uui-box>
          `,
        )}
      </div>
    `;
  }

  #renderTextCard(label: string, value?: string, emptyLabel = "No content.") {
    return html`
      <div class="property-row">
        <div class="property-label">${label}</div>
        <div class="property-editor">
          <div class="property-value">${value?.trim() ? value : emptyLabel}</div>
        </div>
      </div>
    `;
  }

  #renderMetaCard(label: string, value?: string) {
    return html`
      <div class="property-row">
        <div class="property-label">${label}</div>
        <div class="property-editor">
          <div class="property-value">${value?.trim() ? value : "Not recorded."}</div>
        </div>
      </div>
    `;
  }

  #renderMetaRow(label: string, value?: string) {
    return html`
      <div class="property-row">
        <div class="property-label">${label}</div>
        <div class="property-editor">
          <pre>${value?.trim() ? value : "Not recorded."}</pre>
        </div>
      </div>
    `;
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

export default RazorSearchStatusModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "razor-search-status-modal": RazorSearchStatusModalElement;
  }
}
