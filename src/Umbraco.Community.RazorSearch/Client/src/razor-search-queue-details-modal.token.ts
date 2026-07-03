import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { RazorSearchQueueBatchDetailsResponse } from "./razor-search-management.client.js";

export type RazorSearchQueueBatchFilter =
  | "all"
  | "queued"
  | "running"
  | "completed"
  | "failed"
  | "cancelled";

export type RazorSearchQueueDetailsModalData = {
  headline: string;
  filter: RazorSearchQueueBatchFilter;
  details: RazorSearchQueueBatchDetailsResponse;
};

export type RazorSearchQueueDetailsModalValue = {
  action: "close";
};

export const RAZOR_SEARCH_QUEUE_DETAILS_MODAL_ALIAS =
  "Umbraco.Community.RazorSearch.Modal.QueueBatchDetails";

export const RAZOR_SEARCH_QUEUE_DETAILS_MODAL =
  new UmbModalToken<
    RazorSearchQueueDetailsModalData,
    RazorSearchQueueDetailsModalValue
  >(RAZOR_SEARCH_QUEUE_DETAILS_MODAL_ALIAS, {
    modal: {
      type: "sidebar",
      size: "medium",
    },
  });
