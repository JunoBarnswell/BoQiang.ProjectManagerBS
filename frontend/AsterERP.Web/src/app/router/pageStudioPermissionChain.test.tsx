// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { usePermissionStore } from "../../core/state";
import { PermissionButton } from "../../shared/auth/PermissionButton";

import { PermissionRoute } from "./PermissionRoute";

vi.mock("../../shared/status/Page403", () => ({ Page403: () => <span>403</span> }));

describe("Page Studio permission chain", () => {
  afterEach(() => {
    cleanup();
    usePermissionStore.setState({ permissionCodes: [] });
  });

  it("renders the view entry only when designer:view is granted", () => {
    usePermissionStore.setState({ permissionCodes: ["app:development-center:designer:view"] });

    render(
      <PermissionRoute permissionCode="app:development-center:designer:view">
        <span>Page Studio</span>
      </PermissionRoute>,
    );

    expect(screen.getByText("Page Studio")).toBeTruthy();
  });

  it("denies the route and hides edit/preview/publish buttons when permissions are missing", () => {
    render(
      <>
        <PermissionRoute permissionCode="app:development-center:designer:view">
          <span>Page Studio</span>
        </PermissionRoute>
        <PermissionButton code="app:development-center:designer:edit">Edit</PermissionButton>
        <PermissionButton code="app:development-center:designer:preview">Preview</PermissionButton>
        <PermissionButton code="app:development-center:designer:publish">Publish</PermissionButton>
      </>,
    );

    expect(screen.queryByText("Page Studio")).toBeNull();
    expect(screen.queryByRole("button", { name: "Edit" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Preview" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Publish" })).toBeNull();
  });
});
