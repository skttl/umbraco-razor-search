import { UmbContextBase } from "@umbraco-cms/backoffice/class-api";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import { UmbViewContext } from "@umbraco-cms/backoffice/view";
import { UMB_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import {
  RAZOR_SEARCH_ENTITY_TYPE,
  RAZOR_SEARCH_WORKSPACE_ALIAS,
} from "./razor-search-backoffice.constants.js";

export class RazorSearchWorkspaceContext extends UmbContextBase {
  readonly workspaceAlias = RAZOR_SEARCH_WORKSPACE_ALIAS;
  readonly view = new UmbViewContext(this, null);

  constructor(host: UmbControllerHost) {
    super(host, UMB_WORKSPACE_CONTEXT);

    this.view.setTitle("RazorSearch operations");
  }

  getEntityType() {
    return RAZOR_SEARCH_ENTITY_TYPE;
  }

  getEntityName() {
    return "RazorSearch operations";
  }
}

export { RazorSearchWorkspaceContext as api };
