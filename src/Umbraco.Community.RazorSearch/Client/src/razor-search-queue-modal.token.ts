import { UmbModalToken } from "@umbraco-cms/backoffice/modal";

export type RazorSearchQueueModalData = {
  headline: string;
};

export type RazorSearchQueueModalValue = {
  action: "self" | "descendants" | "cancel";
};

export const RAZOR_SEARCH_QUEUE_MODAL_ALIAS =
  "Umbraco.Community.RazorSearch.Modal.QueueDocument";

export const RAZOR_SEARCH_QUEUE_MODAL =
  new UmbModalToken<RazorSearchQueueModalData, RazorSearchQueueModalValue>(
    RAZOR_SEARCH_QUEUE_MODAL_ALIAS,
    {
      modal: {
        type: "dialog",
        size: "small",
      },
    },
  );
