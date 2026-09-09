import type { UmbDocumentUserPermissionConditionConfig } from "@umbraco-cms/backoffice/document";
import type { ManifestEntityAction } from "@umbraco-cms/backoffice/entity-action";
import type { ManifestMenuItem } from "@umbraco-cms/backoffice/menu";
import type { ManifestModal } from "@umbraco-cms/backoffice/modal";
import type {
  ManifestWorkspace,
  ManifestWorkspaceView,
} from "@umbraco-cms/backoffice/workspace";
import { UMB_WORKSPACE_CONDITION_ALIAS } from "@umbraco-cms/backoffice/workspace";
import {
  RAZOR_SEARCH_ADVANCED_SETTINGS_MENU_ALIAS,
  RAZOR_SEARCH_ENTITY_TYPE,
  RAZOR_SEARCH_WORKSPACE_ALIAS,
} from "./razor-search-backoffice.constants.js";
import { RAZOR_SEARCH_QUEUE_DETAILS_MODAL_ALIAS } from "./razor-search-queue-details-modal.token.js";
import { RAZOR_SEARCH_QUEUE_MODAL_ALIAS } from "./razor-search-queue-modal.token.js";
import { RAZOR_SEARCH_STATUS_MODAL_ALIAS } from "./razor-search-status-modal.token.js";

const manifests: Array<
  | ManifestEntityAction
  | ManifestMenuItem
  | ManifestModal
  | ManifestWorkspace
  | ManifestWorkspaceView
> = [
  {
    type: "entityAction",
    kind: "default",
    alias: "Umbraco.Community.RazorSearch.EntityAction",
    name: "RazorSearch Entity Action",
    weight: 200,
    api: () => import("./razor-search-entity-action.js"),
    forEntityTypes: ["document"],
    conditions: [{ alias: "Umb.Condition.UserPermission.Document", allOf: ["Umb.Document.Publish"] } satisfies UmbDocumentUserPermissionConditionConfig],
    meta: {
      icon: "icon-search",
      label: "Queue RazorSearch job",
    },
  },
  {
    type: "modal",
    alias: RAZOR_SEARCH_QUEUE_MODAL_ALIAS,
    name: "RazorSearch Queue Modal",
    element: () => import("./razor-search-queue-modal.element.js"),
  },
  {
    type: "modal",
    alias: RAZOR_SEARCH_QUEUE_DETAILS_MODAL_ALIAS,
    name: "RazorSearch Queue Details Modal",
    element: () => import("./razor-search-queue-details-modal.element.js"),
  },
  {
    type: "modal",
    alias: RAZOR_SEARCH_STATUS_MODAL_ALIAS,
    name: "RazorSearch Status Modal",
    element: () => import("./razor-search-status-modal.element.js"),
  },
  {
    type: "menuItem",
    alias: "Umbraco.Community.RazorSearch.MenuItem.Settings",
    name: "RazorSearch Settings Menu Item",
    conditions: [{ alias: "Umb.Condition.CurrentUser.IsAdmin" }],
    weight: 401,
    meta: {
      label: "RazorSearch",
      icon: "icon-search",
      entityType: RAZOR_SEARCH_ENTITY_TYPE,
      menus: [RAZOR_SEARCH_ADVANCED_SETTINGS_MENU_ALIAS],
    },
  },
  {
    type: "workspace",
    alias: RAZOR_SEARCH_WORKSPACE_ALIAS,
    name: "RazorSearch Workspace",
    element: () => import("./razor-search-workspace.element.js"),
    api: () => import("./razor-search-workspace.context.js"),
    meta: {
      entityType: RAZOR_SEARCH_ENTITY_TYPE,
    },
  },
  {
    type: "workspaceView",
    alias: "Umbraco.Community.RazorSearch.WorkspaceView.Management",
    name: "RazorSearch Management Workspace View",
    element: () => import("./razor-search-management-section-view.element.js"),
    weight: 300,
    meta: {
      label: "Operations",
      pathname: "management",
      icon: "icon-search",
    },
    conditions: [
      { alias: "Umb.Condition.CurrentUser.IsAdmin" },
      {
        alias: UMB_WORKSPACE_CONDITION_ALIAS,
        match: RAZOR_SEARCH_WORKSPACE_ALIAS,
      },
    ],
  },
];

export { manifests };
