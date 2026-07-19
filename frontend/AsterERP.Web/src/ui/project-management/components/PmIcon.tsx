import { IconArchive, IconArrowBackUp, IconArrowForwardUp, IconCalendar, IconCheck, IconChevronDown, IconCircleCheck, IconDots, IconFilter, IconFolder, IconHeart, IconLayoutSidebarLeftCollapse, IconLayoutSidebarLeftExpand, IconPlus, IconSearch, IconSettings2, IconSparkles, IconStar, IconTrash, IconUser } from '@tabler/icons-react';
import type { ComponentType } from 'react';

const icons = { archive: IconArchive, back: IconArrowBackUp, calendar: IconCalendar, check: IconCheck, chevron: IconChevronDown, circleCheck: IconCircleCheck, dots: IconDots, filter: IconFilter, folder: IconFolder, forward: IconArrowForwardUp, heart: IconHeart, plus: IconPlus, search: IconSearch, settings: IconSettings2, sidebarClose: IconLayoutSidebarLeftCollapse, sidebarOpen: IconLayoutSidebarLeftExpand, sparkles: IconSparkles, star: IconStar, trash: IconTrash, user: IconUser } satisfies Record<string, ComponentType<{ size?: number; stroke?: number }>>;

export type PmIconName = keyof typeof icons;
export function PmIcon({ name, size = 16 }: { name: PmIconName; size?: number }) { const Icon = icons[name]; return <Icon aria-hidden="true" size={size} stroke={1.8} />; }
