import { css, customElement, html } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type {
  UmbModalContext,
  UmbModalExtensionElement,
} from "@umbraco-cms/backoffice/modal";
import type {
  RazorSearchQueueModalData,
  RazorSearchQueueModalValue,
} from "./razor-search-queue-modal.token.js";

@customElement("razor-search-queue-modal")
export class RazorSearchQueueModalElement
  extends UmbLitElement
  implements
    UmbModalExtensionElement<
      RazorSearchQueueModalData,
      RazorSearchQueueModalValue
    >
{
  declare modalContext: UmbModalContext<
    RazorSearchQueueModalData,
    RazorSearchQueueModalValue
  > | undefined;

  static override properties = {
    modalContext: { attribute: false },
  };

  #submit(action: RazorSearchQueueModalValue["action"]) {
    this.modalContext?.updateValue({ action });
    this.modalContext?.submit();
  }

  override render() {
    const headline =
      this.modalContext?.data.headline ?? "Queue RazorSearch job";

    return html`
      <umb-body-layout .headline=${headline}>
        <div class="content">
          <p>
            Choose whether RazorSearch should queue only this document or also
            include all descendants.
          </p>
        </div>

        <div slot="actions" class="actions">
          <uui-button
            look="secondary"
            label="Cancel"
            @click=${() => this.#submit("cancel")}></uui-button>
          <uui-button
            look="default"
            color="positive"
            label="Queue this document"
            @click=${() => this.#submit("self")}></uui-button>
          <uui-button
            look="primary"
            color="positive"
            label="Queue document and descendants"
            @click=${() => this.#submit("descendants")}></uui-button>
        </div>
      </umb-body-layout>
    `;
  }

  static override styles = [
    css`
      :host {
        display: block;
      }

      .content {
        max-width: 42rem;
      }

      p {
        margin: 0;
        color: var(--uui-color-text);
        line-height: 1.5;
      }

      .actions {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-3);
        justify-content: flex-end;
      }
    `,
  ];
}

export default RazorSearchQueueModalElement;

declare global {
  interface HTMLElementTagNameMap {
    "razor-search-queue-modal": RazorSearchQueueModalElement;
  }
}
