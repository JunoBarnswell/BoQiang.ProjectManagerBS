export const RESOURCE_DROP_EVENT = 'astererp:resource-drop';

export interface ResourceDropEventDetail {
  bindingSlot?: string;
  resourceId: string;
}

export function createResourceDropEvent(resourceId: string, bindingSlot?: string): CustomEvent<ResourceDropEventDetail> {
  return new CustomEvent<ResourceDropEventDetail>(RESOURCE_DROP_EVENT, {
    bubbles: true,
    detail: { bindingSlot, resourceId }
  });
}
