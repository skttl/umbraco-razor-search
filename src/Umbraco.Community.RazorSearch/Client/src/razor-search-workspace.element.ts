import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { css, html } from "@umbraco-cms/backoffice/external/lit";

export class RazorSearchWorkspaceElement extends UmbLitElement {
  static styles = css`
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
  `;

  override render() {
    return html`
      <umb-workspace-editor
        headline="RazorSearch operations"
        .enforceNoFooter=${true}></umb-workspace-editor>
    `;
  }
}

customElements.define("razor-search-workspace", RazorSearchWorkspaceElement);

export { RazorSearchWorkspaceElement as element };
export default RazorSearchWorkspaceElement;
