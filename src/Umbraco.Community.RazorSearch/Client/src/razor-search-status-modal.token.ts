import { UmbModalToken } from "@umbraco-cms/backoffice/modal";
import type { RazorSearchDocumentStatusResponse } from "./razor-search-management.client.js";

export type RazorSearchStatusModalData = {
  headline: string;
  status: RazorSearchDocumentStatusResponse;
};

export type RazorSearchStatusModalValue = {
  action: "close" | "queue";
};

export const RAZOR_SEARCH_STATUS_MODAL_ALIAS =
  "Umbraco.Community.RazorSearch.Modal.DocumentStatus";

export const RAZOR_SEARCH_STATUS_MODAL =
  new UmbModalToken<RazorSearchStatusModalData, RazorSearchStatusModalValue>(
    RAZOR_SEARCH_STATUS_MODAL_ALIAS,
    {
      modal: {
        type: "sidebar",
        size: "medium",
      },
    },
  );
