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
  RazorSearchDocumentIndexEntryResponse,
  RazorSearchDocumentSnapshotResponse,
  RazorSearchDocumentStatusResponse,
  RazorSearchQueueJobResponse,
} from "./razor-search-management.client.js";
import type {
  RazorSearchStatusModalData,
  RazorSearchStatusModalValue,
} from "./razor-search-status-modal.token.js";

type StatusTab = "snapshot" | "indexed" | "diff" | "info";

type VariantComparison = {
  key: string;
  label: string;
  snapshot?: RazorSearchDocumentSnapshotResponse;
  indexedEntry?: RazorSearchDocumentIndexEntryResponse;
};

type DiffRow = {
  kind: "equal" | "removed" | "added";
  value: string;
};

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
    _showAllJobs: { state: true },
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
      .metric-card,
      .job-list,
      .job-card,
      .tab-layout,
      .content-stack,
      .meta-grid,
      .diff-stack,
      .diff-section,
      .empty-state {
        display: grid;
        gap: var(--uui-size-space-4);
        min-width: 0;
      }

      .layout {
        padding-bottom: var(--uui-size-space-4);
      }

      .summary-header,
      .summary-title,
      .summary-updated,
      .job-header,
      .variant-header,
      .tab-header,
      .diff-header,
      .actions {
        display: flex;
        gap: var(--uui-size-space-3);
        align-items: center;
        flex-wrap: wrap;
        min-width: 0;
      }

      .summary-header,
      .job-header,
      .variant-header,
      .diff-header {
        justify-content: space-between;
      }

      .summary-title,
      .summary-updated {
        min-width: 0;
      }

      .summary-title h3,
      .section-title,
      .tab-title,
      .meta-title,
      .diff-title {
        margin: 0;
      }

      .summary-title h3,
      .job-header strong,
      .variant-header strong {
        min-width: 0;
        overflow-wrap: anywhere;
      }

      .section-title {
        font-size: 1rem;
      }

      .summary-updated,
      .meta,
      .muted,
      .field-hint,
      .empty-copy {
        color: var(--uui-color-text-alt);
      }

      .metric-grid {
        grid-template-columns: minmax(0, 1fr);
      }

      .metric-card {
        gap: var(--uui-size-space-2);
        padding: var(--uui-size-space-4);
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
      }

      .metric-label {
        color: var(--uui-color-text-alt);
        font-size: 0.85rem;
      }

      .metric-value {
        font-size: 1.6rem;
        font-weight: 700;
        line-height: 1;
      }

      .job-card,
      .variant-card {
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        padding: var(--uui-size-space-4);
        min-width: 0;
      }

      .variant-card {
        display: grid;
        gap: var(--uui-size-space-4);
      }

      .variant-meta {
        display: grid;
        gap: var(--uui-size-space-2);
        min-width: 0;
      }

      .tab-layout {
        overflow-x: hidden;
      }

      .tab-strip {
        padding-bottom: var(--uui-size-space-2);
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
        overflow-x: hidden;
      }

      .content-grid,
      .meta-grid {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
        gap: var(--uui-size-space-3);
        min-width: 0;
      }

      .content-card,
      .meta-card {
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        background: var(--uui-color-surface);
        padding: var(--uui-size-space-4);
        min-width: 0;
      }

      .meta-card {
        display: grid;
        gap: var(--uui-size-space-2);
      }

      .meta-label {
        margin: 0;
        color: var(--uui-color-text-alt);
        font-size: 0.76rem;
        font-weight: 700;
        letter-spacing: 0.04em;
        text-transform: uppercase;
      }

      .meta-value {
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

      .diff-block {
        border: 1px solid var(--uui-color-divider);
        border-radius: var(--uui-border-radius);
        overflow: hidden;
        background: var(--uui-color-surface);
        min-width: 0;
      }

      .diff-row {
        display: grid;
        grid-template-columns: auto minmax(0, 1fr);
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
        border-top: 1px solid var(--uui-color-divider);
        min-width: 0;
      }

      .diff-row:first-child {
        border-top: 0;
      }

      .diff-row--added {
        background: color-mix(
          in srgb,
          var(--uui-color-positive-standalone) 8%,
          var(--uui-color-surface)
        );
      }

      .diff-row--removed {
        background: color-mix(
          in srgb,
          var(--uui-color-danger-standalone) 8%,
          var(--uui-color-surface)
        );
      }

      .diff-marker {
        width: 1rem;
        text-align: center;
        font-weight: 700;
        color: var(--uui-color-text-alt);
      }

      pre,
      .diff-row code {
        margin: 0;
        max-width: 100%;
        overflow-wrap: anywhere;
        white-space: pre-wrap;
        word-break: break-word;
        font-family: ui-monospace, SFMono-Regular, Consolas, monospace;
        font-size: 0.84rem;
        line-height: 1.5;
      }

      pre {
        max-height: 20rem;
        overflow: auto;
      }

      .diff-row code {
        display: block;
        background: transparent;
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
        justify-content: flex-end;
      }

      .secondary-action {
        justify-self: start;
      }

      @media (min-width: 720px) {
        .metric-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }

        .content-grid,
        .meta-grid {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }
    `,
  ];

  declare _activeTab: StatusTab;
  declare _showAllJobs: boolean;

  constructor() {
    super();
    this._activeTab = "snapshot";
    this._showAllJobs = false;
  }

  #submit() {
    this.modalContext?.updateValue({ action: "close" });
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
      tab === "indexed" ||
      tab === "diff" ||
      tab === "info"
    ) {
      this.#setActiveTab(tab);
    }
  }

  #toggleJobs() {
    this._showAllJobs = !this._showAllJobs;
  }

  override render() {
    const headline =
      this.modalContext?.data.headline ?? "RazorSearch document status";
    const status = this.modalContext?.data.status;

    if (!status) {
      return html`
        <umb-body-layout .headline=${headline}>
          <div class="layout">
            ${this.#renderEmptyState(
              "No status payload was supplied for this dialog.",
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

    return html`
      <umb-body-layout .headline=${headline}>
        <div class="layout">
          ${this.#renderSummary(status)}
          ${this.#renderJobs(status.jobs)}
          ${this.#renderTabs(status)}
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

  #renderSummary(status: RazorSearchDocumentStatusResponse) {
    return html`
      <uui-box>
        <div slot="headline" class="summary-header">
          <div class="summary-title">
            <h3>${status.documentName?.trim() || status.documentId}</h3>
            <uui-badge color=${this.#badgeColor(status.state)}>
              ${status.state}
            </uui-badge>
          </div>
          <div class="summary-updated">
            Updated: ${this.#formatDate(status.updatedAt) ?? "Unknown"}
          </div>
        </div>

        <div class="stack">
          ${status.message ? html`<p>${status.message}</p>` : nothing}

          <div class="metric-grid">
            ${this.#renderMetric("Pending", String(status.pendingDocumentCount))}
            ${this.#renderMetric(
              "Completed snapshots",
              String(status.completedDocumentCount),
            )}
            ${this.#renderMetric("Failed", String(status.failedDocumentCount))}
            ${this.#renderMetric(
              "Includes descendants",
              status.includeDescendants ? "Yes" : "No",
            )}
          </div>
        </div>
      </uui-box>
    `;
  }

  #renderMetric(label: string, value: string) {
    return html`
      <div class="metric-card">
        <div class="metric-label">${label}</div>
        <div class="metric-value">${value}</div>
      </div>
    `;
  }

  #renderJobs(jobs: RazorSearchQueueJobResponse[]) {
    const visibleJobs = this._showAllJobs ? jobs : jobs.slice(0, 1);
    const hiddenJobCount = Math.max(jobs.length - 1, 0);

    return html`
      <uui-box>
        <h4 slot="headline" class="section-title">Queued Jobs</h4>

        ${jobs.length === 0
          ? this.#renderEmptyState(
              "No queued or historical in-memory jobs are available for this document.",
            )
          : html`
              <div class="job-list">
                ${visibleJobs.map(
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
                            : ""}${job.segment ? ` • Segment: ${job.segment}` : ""}
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

                ${jobs.length > 1
                  ? html`
                      <uui-button
                        class="secondary-action"
                        look="secondary"
                        color="default"
                        @click=${() => this.#toggleJobs()}>
                        ${this._showAllJobs
                          ? "Show fewer jobs"
                          : `Show ${hiddenJobCount} more job${hiddenJobCount === 1 ? "" : "s"}`}
                      </uui-button>
                    `
                  : nothing}
              </div>
            `}
      </uui-box>
    `;
  }

  #renderTabs(status: RazorSearchDocumentStatusResponse) {
    return html`
      <uui-box>
        <div slot="headline" class="tab-header">
          <h4 class="section-title">Snapshot Details</h4>
        </div>

        <div class="tab-layout">
          <div class="tab-strip">
            <uui-tab-group
              role="tablist"
              aria-label="RazorSearch status tabs"
              @click=${this.#onTabChange}>
              ${this.#renderTab("snapshot", "Snapshot")}
              ${this.#renderTab("indexed", "Indexed")}
              ${this.#renderTab("diff", "Diff")}
              ${this.#renderTab("info", "Info")}
            </uui-tab-group>
          </div>

          <div class="tab-panel">${this.#renderActiveTab(status)}</div>
        </div>
      </uui-box>
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
      case "indexed":
        return this.#renderIndexedTab(status.indexedEntries);
      case "diff":
        return this.#renderDiffTab(status.snapshots, status.indexedEntries);
      case "info":
        return this.#renderInfoTab(status.snapshots);
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
        ${snapshots.map(
          (snapshot) => html`
            <article class="variant-card">
              <div class="content-grid">
                ${this.#renderTextCard("Title text", snapshot.titleText)}
                ${this.#renderTextCard("Summary text", snapshot.summaryText)}
                ${this.#renderTextCard("Heading text", snapshot.headingText)}
                ${this.#renderTextCard("Body text", snapshot.bodyText)}
              </div>

              <div class="variant-meta">
                <div class="variant-header">
                  <strong>${this.#variantLabel(snapshot)}</strong>
                  <uui-badge color=${this.#badgeColor(snapshot.state)}>
                    ${snapshot.state}
                  </uui-badge>
                </div>

                <p class="meta">
                  Renderer: ${snapshot.renderer}${snapshot.culture
                    ? ` • Culture: ${snapshot.culture}`
                    : ""}${snapshot.segment ? ` • Segment: ${snapshot.segment}` : ""}
                </p>

                ${snapshot.errorMessage
                  ? html`<div class="notice"><p>${snapshot.errorMessage}</p></div>`
                  : nothing}
              </div>
            </article>
          `,
        )}
      </div>
    `;
  }

  #renderIndexedTab(indexedEntries: RazorSearchDocumentIndexEntryResponse[]) {
    if (indexedEntries.length === 0) {
      return this.#renderEmptyState(
        "No successful snapshot content is currently contributing RazorSearch fields to the index.",
      );
    }

    return html`
      <div class="content-stack">
        ${indexedEntries.map(
          (entry) => html`
            <article class="variant-card">
              <div class="content-grid">
                ${this.#renderTextCard(
                  "Title fields",
                  this.#joinValues(entry.titles),
                )}
                ${this.#renderTextCard(
                  "Summary fields",
                  this.#joinValues(entry.summaries),
                )}
                ${this.#renderTextCard(
                  "Heading fields",
                  this.#joinValues(entry.headings),
                )}
                ${this.#renderTextCard(
                  "Content fields",
                  this.#joinValues(entry.content),
                )}
              </div>

              <div class="variant-meta">
                <div class="variant-header">
                  <strong>${this.#variantLabel(entry)}</strong>
                </div>
              </div>
            </article>
          `,
        )}
      </div>
    `;
  }

  #renderDiffTab(
    snapshots: RazorSearchDocumentSnapshotResponse[],
    indexedEntries: RazorSearchDocumentIndexEntryResponse[],
  ) {
    const comparisons = this.#buildComparisons(snapshots, indexedEntries);

    if (comparisons.length === 0) {
      return this.#renderEmptyState(
        "No snapshot or index content is available to compare yet.",
      );
    }

    return html`
      <div class="diff-stack">
        ${comparisons.map(
          (comparison) => html`
            <article class="variant-card">
              ${this.#renderDiffSection(
                "Title",
                comparison.snapshot?.titleText,
                this.#joinValues(comparison.indexedEntry?.titles ?? []),
              )}
              ${this.#renderDiffSection(
                "Summary",
                comparison.snapshot?.summaryText,
                this.#joinValues(comparison.indexedEntry?.summaries ?? []),
              )}
              ${this.#renderDiffSection(
                "Heading",
                comparison.snapshot?.headingText,
                this.#joinValues(comparison.indexedEntry?.headings ?? []),
              )}
              ${this.#renderDiffSection(
                "Content",
                comparison.snapshot?.bodyText,
                this.#joinValues(comparison.indexedEntry?.content ?? []),
              )}

              <div class="variant-meta">
                <div class="variant-header">
                  <strong>${comparison.label}</strong>
                  ${comparison.snapshot
                    ? html`
                        <uui-badge
                          color=${this.#badgeColor(comparison.snapshot.state)}>
                          ${comparison.snapshot.state}
                        </uui-badge>
                      `
                    : nothing}
                </div>
              </div>
            </article>
          `,
        )}
      </div>
    `;
  }

  #renderInfoTab(snapshots: RazorSearchDocumentSnapshotResponse[]) {
    if (snapshots.length === 0) {
      return this.#renderEmptyState(
        "No stored snapshots were found for this document.",
      );
    }

    return html`
      <div class="content-stack">
        ${snapshots.map(
          (snapshot) => html`
            <article class="variant-card">
              <div class="meta-grid">
                ${this.#renderMetaCard("URL", snapshot.route)}
                ${this.#renderMetaCard("Final URL", snapshot.finalUrl)}
                ${this.#renderMetaCard("Renderer", snapshot.renderer)}
                ${this.#renderMetaCard(
                  "Render date",
                  this.#formatDate(snapshot.renderedAt),
                )}
                ${this.#renderMetaCard(
                  "Update date",
                  this.#formatDate(snapshot.updatedAt),
                )}
                ${this.#renderMetaCard("Culture", snapshot.culture ?? "Invariant")}
                ${this.#renderMetaCard("Segment", snapshot.segment ?? "None")}
              </div>

              ${this.#renderTextCard(
                "Full snapshot HTML",
                snapshot.snapshotHtml,
                "No HTML snapshot is stored yet for this entry.",
              )}

              <div class="variant-meta">
                <div class="variant-header">
                  <strong>${this.#variantLabel(snapshot)}</strong>
                  <uui-badge color=${this.#badgeColor(snapshot.state)}>
                    ${snapshot.state}
                  </uui-badge>
                </div>
              </div>
            </article>
          `,
        )}
      </div>
    `;
  }

  #renderDiffSection(label: string, snapshotValue?: string, indexedValue?: string) {
    const normalizedSnapshot = snapshotValue?.trim() || "";
    const normalizedIndexed = indexedValue?.trim() || "";
    const matches = normalizedSnapshot === normalizedIndexed;

    return html`
      <section class="diff-section">
        <div class="diff-header">
          <h5 class="diff-title">${label}</h5>
          <span class="field-hint">${matches ? "Matches index" : "Differences found"}</span>
        </div>

        ${matches
          ? this.#renderTextCard(
              `${label} content`,
              normalizedSnapshot || normalizedIndexed,
            )
          : html`
              <div class="diff-block">
                ${this.#buildDiffRows(normalizedSnapshot, normalizedIndexed).map(
                  (row) => html`
                    <div class="diff-row diff-row--${row.kind}">
                      <span class="diff-marker">${this.#diffMarker(row.kind)}</span>
                      <code>${row.value}</code>
                    </div>
                  `,
                )}
              </div>
            `}
      </section>
    `;
  }

  #renderTextCard(label: string, value?: string, emptyLabel = "No content.") {
    return html`
      <section class="content-card">
        <h5 class="meta-title">${label}</h5>
        <pre>${value?.trim() ? value : emptyLabel}</pre>
      </section>
    `;
  }

  #renderMetaCard(label: string, value?: string) {
    return html`
      <section class="meta-card">
        <p class="meta-label">${label}</p>
        <p class="meta-value">${value?.trim() ? value : "Not recorded."}</p>
      </section>
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

  #buildComparisons(
    snapshots: RazorSearchDocumentSnapshotResponse[],
    indexedEntries: RazorSearchDocumentIndexEntryResponse[],
  ): VariantComparison[] {
    const comparisons = new Map<string, VariantComparison>();

    for (const snapshot of snapshots) {
      const key = this.#variantKey(snapshot.culture, snapshot.segment);
      comparisons.set(key, {
        key,
        label: this.#variantLabel(snapshot),
        snapshot,
        indexedEntry: comparisons.get(key)?.indexedEntry,
      });
    }

    for (const entry of indexedEntries) {
      const key = this.#variantKey(entry.culture, entry.segment);
      comparisons.set(key, {
        key,
        label: comparisons.get(key)?.label ?? this.#variantLabel(entry),
        snapshot: comparisons.get(key)?.snapshot,
        indexedEntry: entry,
      });
    }

    return Array.from(comparisons.values()).sort((left, right) =>
      left.label.localeCompare(right.label),
    );
  }

  #variantKey(culture?: string, segment?: string) {
    return `${culture?.trim().toLowerCase() || "invariant"}::${segment
      ?.trim()
      .toLowerCase() || "none"}`;
  }

  #variantLabel(
    entry:
      | Pick<RazorSearchDocumentSnapshotResponse, "route" | "culture" | "segment">
      | Pick<RazorSearchDocumentIndexEntryResponse, "culture" | "segment">,
  ) {
    const parts = [];

    if ("route" in entry && entry.route) {
      parts.push(entry.route);
    }

    parts.push(entry.culture || "Invariant");

    if (entry.segment) {
      parts.push(entry.segment);
    }

    return parts.join(" • ");
  }

  #buildDiffRows(snapshotValue: string, indexedValue: string): DiffRow[] {
    const snapshotLines = this.#toDiffLines(snapshotValue);
    const indexedLines = this.#toDiffLines(indexedValue);

    if (snapshotLines.length === 0 && indexedLines.length === 0) {
      return [{ kind: "equal", value: "No content." }];
    }

    if (snapshotLines.length * indexedLines.length > 5000) {
      return [
        ...(snapshotValue
          ? [{ kind: "removed", value: snapshotValue } satisfies DiffRow]
          : []),
        ...(indexedValue
          ? [{ kind: "added", value: indexedValue } satisfies DiffRow]
          : []),
      ];
    }

    const matrix = Array.from({ length: snapshotLines.length + 1 }, () =>
      Array<number>(indexedLines.length + 1).fill(0),
    );

    for (let leftIndex = snapshotLines.length - 1; leftIndex >= 0; leftIndex -= 1) {
      for (
        let rightIndex = indexedLines.length - 1;
        rightIndex >= 0;
        rightIndex -= 1
      ) {
        matrix[leftIndex][rightIndex] =
          snapshotLines[leftIndex] === indexedLines[rightIndex]
            ? matrix[leftIndex + 1][rightIndex + 1] + 1
            : Math.max(
                matrix[leftIndex + 1][rightIndex],
                matrix[leftIndex][rightIndex + 1],
              );
      }
    }

    const rows: DiffRow[] = [];
    let leftIndex = 0;
    let rightIndex = 0;

    while (leftIndex < snapshotLines.length && rightIndex < indexedLines.length) {
      if (snapshotLines[leftIndex] === indexedLines[rightIndex]) {
        rows.push({ kind: "equal", value: snapshotLines[leftIndex] });
        leftIndex += 1;
        rightIndex += 1;
        continue;
      }

      if (matrix[leftIndex + 1][rightIndex] >= matrix[leftIndex][rightIndex + 1]) {
        rows.push({ kind: "removed", value: snapshotLines[leftIndex] });
        leftIndex += 1;
        continue;
      }

      rows.push({ kind: "added", value: indexedLines[rightIndex] });
      rightIndex += 1;
    }

    while (leftIndex < snapshotLines.length) {
      rows.push({ kind: "removed", value: snapshotLines[leftIndex] });
      leftIndex += 1;
    }

    while (rightIndex < indexedLines.length) {
      rows.push({ kind: "added", value: indexedLines[rightIndex] });
      rightIndex += 1;
    }

    return rows;
  }

  #toDiffLines(value: string) {
    const normalized = value.trim();
    if (!normalized) {
      return [];
    }

    return normalized
      .replace(/\r/g, "")
      .split(/\n+/)
      .flatMap((line) => this.#chunkDiffLine(line))
      .filter((line) => line.length > 0);
  }

  #chunkDiffLine(value: string) {
    const normalized = value.trim();
    if (!normalized) {
      return [];
    }

    const sentences = normalized
      .split(/(?<=[.!?])\s+/)
      .map((sentence) => sentence.trim())
      .filter((sentence) => sentence.length > 0);

    const chunks = sentences.length > 0 ? sentences : [normalized];
    const wrapped: string[] = [];

    for (const chunk of chunks) {
      const words = chunk.split(/\s+/);
      for (let index = 0; index < words.length; index += 18) {
        wrapped.push(words.slice(index, index + 18).join(" "));
      }
    }

    return wrapped;
  }

  #diffMarker(kind: DiffRow["kind"]) {
    if (kind === "added") {
      return "+";
    }

    if (kind === "removed") {
      return "-";
    }

    return "=";
  }

  #joinValues(values: string[]) {
    return values.length > 0 ? values.join("\n\n") : undefined;
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
