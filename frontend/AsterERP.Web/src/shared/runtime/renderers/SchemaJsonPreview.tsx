interface SchemaJsonPreviewProps {
  modelCode?: unknown;
  pageCode?: unknown;
  pageName?: unknown;
  pageType?: unknown;
  permissionCode?: unknown;
}

function stringifyProps(props: SchemaJsonPreviewProps): string {
  return JSON.stringify(
    {
      pageCode: props.pageCode ?? null,
      pageName: props.pageName ?? null,
      pageType: props.pageType ?? null,
      modelCode: props.modelCode ?? null,
      permissionCode: props.permissionCode ?? null
    },
    null,
    2
  );
}

export function SchemaJsonPreview(props: SchemaJsonPreviewProps) {
  return (
    <pre className="max-h-72 overflow-auto rounded border border-gray-200 bg-gray-50 p-3 text-xs leading-5 text-gray-700">
      {stringifyProps(props)}
    </pre>
  );
}
