import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type { UmbEntityActionArgs } from "@umbraco-cms/backoffice/entity-action";
import { UmbEntityActionBase } from "@umbraco-cms/backoffice/entity-action";
import { umbOpenModal } from "@umbraco-cms/backoffice/modal";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { RazorSearchManagementClient } from "./razor-search-management.client.js";
import { RAZOR_SEARCH_QUEUE_MODAL } from "./razor-search-queue-modal.token.js";

export class RazorSearchEntityAction extends UmbEntityActionBase<never> {
  readonly #managementClient: RazorSearchManagementClient;

  constructor(host: UmbControllerHost, args: UmbEntityActionArgs<never>) {
    super(host, args);
    this.#managementClient = new RazorSearchManagementClient(this);
  }

  async execute() {
    if (!this.args.unique) {
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

    const response = await this.#managementClient.queueDocument(
      this.args.unique,
      modalValue.action === "descendants",
    );

    if (!response) {
      return;
    }

    const notificationContext = await this.getContext(UMB_NOTIFICATION_CONTEXT);
    notificationContext?.peek("positive", {
      data: {
        message: response.message,
      },
    });
  }
}

export const api = RazorSearchEntityAction;
