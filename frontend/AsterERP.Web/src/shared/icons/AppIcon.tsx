import type { LucideProps } from 'lucide-react';
import {
  Activity,
  AlertTriangle,
  ArrowDown,
  ArrowLeft,
  ArrowRight,
  ArrowUp,
  Bell,
  BookOpen,
  Boxes,
  Braces,
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  Check,
  CheckCircle2,
  CheckSquare,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  CircleHelp,
  CircleUserRound,
  ClipboardList,
  Clock3,
  Code2,
  Cog,
  Copy,
  Cuboid,
  DatabaseZap,
  Download,
  ExternalLink,
  Eye,
  Factory,
  FileClock,
  FileSearch,
  FileText,
  Files,
  Flag,
  FolderCog,
  Gauge,
  GitBranch,
  Hand,
  HardDrive,
  House,
  Inbox,
  KeyRound,
  LayoutDashboard,
  Mail,
  Megaphone,
  Menu,
  Mic,
  Package,
  PanelLeftClose,
  PanelLeftOpen,
  PauseCircle,
  Pencil,
  PlayCircle,
  Plus,
  RefreshCw,
  Rocket,
  Bot,
  Search,
  ShieldCheck,
  Settings,
  Sidebar,
  SquareKanban,
  SlidersHorizontal,
  StopCircle,
  Table,
  Target,
  Timer,
  UserRound,
  Users,
  Trash2,
  Upload,
  UserCheck,
  UserCog,
  Volume2,
  Wrench,
  X
} from 'lucide-react';
import type { ComponentType } from 'react';

type LucideIcon = ComponentType<LucideProps>;

export type AppIconName =
  | 'activity'
  | 'alertTriangle'
  | 'arrowDown'
  | 'arrowLeft'
  | 'arrowRight'
  | 'arrowUp'
  | 'bell'
  | 'book'
  | 'braces'
  | 'briefcase'
  | 'building'
  | 'calendar'
  | 'check'
  | 'checkCircle'
  | 'checkSquare'
  | 'chevronDown'
  | 'chevronLeft'
  | 'chevronRight'
  | 'clipboard'
  | 'collapse'
  | 'clock'
  | 'code'
  | 'copy'
  | 'cube'
  | 'dashboard'
  | 'default'
  | 'defaultUser'
  | 'download'
  | 'external'
  | 'eye'
  | 'factory'
  | 'fileClock'
  | 'fileSearch'
  | 'files'
  | 'flag'
  | 'gitBranch'
  | 'hand'
  | 'hardDrive'
  | 'home'
  | 'inbox'
  | 'key'
  | 'mail'
  | 'megaphone'
  | 'mic'
  | 'module'
  | 'moduleBox'
  | 'nav'
  | 'package'
  | 'pause'
  | 'play'
  | 'rocket'
  | 'robot'
  | 'roles'
  | 'edit'
  | 'settings'
  | 'shield'
  | 'sidebar'
  | 'sidebarClose'
  | 'sidebarOpen'
  | 'sliders'
  | 'stop'
  | 'system'
  | 'table'
  | 'target'
  | 'timer'
  | 'upload'
  | 'user'
  | 'userCheck'
  | 'userCog'
  | 'users'
  | 'usersGroup'
  | 'usersManagement'
  | 'usersGear'
  | 'volume'
  | 'wrench'
  | 'x'
  | 'database'
  | 'menu'
  | 'moduleManagement'
  | 'systemDicts'
  | 'gear'
  | 'list'
  | 'plus'
  | 'search'
  | 'trash'
  | 'refresh';

type AppIconAliasMap = Record<string, AppIconName>;

const iconRegistry: Record<AppIconName, LucideIcon> = {
  activity: Activity,
  alertTriangle: AlertTriangle,
  arrowDown: ArrowDown,
  arrowLeft: ArrowLeft,
  arrowRight: ArrowRight,
  arrowUp: ArrowUp,
  bell: Bell,
  book: BookOpen,
  braces: Braces,
  briefcase: BriefcaseBusiness,
  building: Building2,
  calendar: CalendarDays,
  check: Check,
  checkCircle: CheckCircle2,
  checkSquare: CheckSquare,
  chevronDown: ChevronDown,
  chevronLeft: ChevronLeft,
  chevronRight: ChevronRight,
  clipboard: ClipboardList,
  collapse: Cog,
  clock: Clock3,
  code: Code2,
  copy: Copy,
  cube: Cuboid,
  dashboard: Gauge,
  default: CircleHelp,
  defaultUser: UserRound,
  download: Download,
  external: ExternalLink,
  eye: Eye,
  factory: Factory,
  fileClock: FileClock,
  fileSearch: FileSearch,
  files: Files,
  flag: Flag,
  gitBranch: GitBranch,
  hand: Hand,
  hardDrive: HardDrive,
  home: House,
  inbox: Inbox,
  key: KeyRound,
  mail: Mail,
  megaphone: Megaphone,
  mic: Mic,
  module: LayoutDashboard,
  moduleBox: Boxes,
  nav: CircleUserRound,
  package: Package,
  pause: PauseCircle,
  play: PlayCircle,
  rocket: Rocket,
  robot: Bot,
  roles: ShieldCheck,
  settings: Settings,
  shield: ShieldCheck,
  sidebar: Sidebar,
  sidebarClose: PanelLeftClose,
  sidebarOpen: PanelLeftOpen,
  sliders: SlidersHorizontal,
  stop: StopCircle,
  system: Wrench,
  table: Table,
  target: Target,
  timer: Timer,
  upload: Upload,
  user: UserRound,
  userCheck: UserCheck,
  userCog: UserCog,
  users: Users,
  usersGroup: Users,
  usersManagement: Users,
  usersGear: UserRound,
  volume: Volume2,
  wrench: Wrench,
  x: X,
  edit: Pencil,
  database: DatabaseZap,
  menu: Menu,
  moduleManagement: FolderCog,
  systemDicts: FileText,
  gear: Cog,
  list: SquareKanban,
  plus: Plus,
  refresh: RefreshCw,
  search: Search,
  trash: Trash2
};

const aliasMap: AppIconAliasMap = {
  'app-window': 'module',
  'arrow-counter-clockwise': 'refresh',
  'arrow-fat-lines-right': 'arrowRight',
  'arrow-left': 'arrowLeft',
  'arrow-right': 'arrowRight',
  'arrowright': 'arrowRight',
  'arrow-square-out': 'external',
  'arrow-u-up-left': 'refresh',
  'arrow-up': 'arrowUp',
  'arrow-down': 'arrowDown',
  'arrows-clockwise': 'refresh',
  at: 'user',
  'bell-ringing': 'bell',
  'book-bookmark': 'book',
  'brackets-curly': 'braces',
  briefcase: 'briefcase',
  buildings: 'building',
  'calendar-dots': 'calendar',
  'caret-down': 'chevronDown',
  'caret-left': 'chevronLeft',
  'caret-line-left': 'sidebarClose',
  'caret-right': 'chevronRight',
  'chart-bar': 'activity',
  'chart-line-up': 'activity',
  check: 'check',
  checks: 'checkCircle',
  'check-circle': 'checkCircle',
  'check-square-offset': 'checkSquare',
  'chat-circle-text': 'mail',
  'clipboard-text': 'clipboard',
  code: 'code',
  copy: 'copy',
  'clock-countdown': 'clock',
  'clock-counter-clockwise': 'clock',
  cube: 'cube',
  database: 'database',
  'download-simple': 'download',
  envelope: 'mail',
  'envelope-simple': 'mail',
  eye: 'eye',
  factory: 'factory',
  'file-clock': 'fileClock',
  'file-search': 'fileSearch',
  filesearch: 'fileSearch',
  'file-text': 'systemDicts',
  files: 'files',
  'flag-checkered': 'flag',
  'floppy-disk': 'systemDicts',
  'flow-arrow': 'gitBranch',
  folder: 'module',
  'git-branch': 'gitBranch',
  hand: 'hand',
  'hand-withdraw': 'hand',
  'hard-drives': 'hardDrive',
  'identification-card': 'user',
  info: 'default',
  key: 'key',
  lightning: 'activity',
  megaphone: 'megaphone',
  microphone: 'mic',
  mic: 'mic',
  'module-management': 'moduleManagement',
  modulemanagement: 'moduleManagement',
  module: 'module',
  modules: 'module',
  'magnifying-glass': 'search',
  menu: 'menu',
  home: 'home',
  house: 'home',
  package: 'package',
  'paper-plane-tilt': 'rocket',
  'pause-circle': 'pause',
  pencil: 'edit',
  'pencil-simple': 'edit',
  play: 'play',
  'play-circle': 'play',
  'plugs-connected': 'wrench',
  'push-pin': 'flag',
  'push-pin-slash': 'flag',
  radar: 'activity',
  robot: 'robot',
  rocket: 'rocket',
  scroll: 'activity',
  'sidebar-simple': 'sidebarOpen',
  'shield-check': 'shield',
  'sign-out': 'external',
  'sliders-horizontal': 'sliders',
  'squares-four': 'moduleBox',
  'stop-circle': 'stop',
  target: 'target',
  textbox: 'table',
  timer: 'timer',
  trash: 'trash',
  tray: 'inbox',
  translate: 'settings',
  'tree-structure': 'module',
  'upload-simple': 'upload',
  volume: 'volume',
  'volume-high': 'volume',
  'user-check': 'userCheck',
  'user-gear': 'userCog',
  'user-switch': 'users',
  warning: 'alertTriangle',
  'warning-circle': 'alertTriangle',
  x: 'x',
  dashboard: 'dashboard',
  settings: 'settings',
  system: 'system',
  'systemdicts': 'systemDicts',
  'system-dicts': 'systemDicts',
  role: 'roles',
  roles: 'roles',
  dict: 'systemDicts',
  dicts: 'systemDicts',
  users: 'users',
  user: 'users',
  admin: 'settings'
};

function normalizeName(name?: string | null): string {
  return (name ?? 'default').trim().toLowerCase();
}

function inferIconByText(name: string): AppIconName | null {
  if (name.includes('user') || name.includes('users')) {
    return 'users';
  }

  if (name.includes('menu') || name.includes('route')) {
    return 'menu';
  }

  if (name.includes('dict') || name.includes('module')) {
    return 'module';
  }

  if (name.includes('system')) {
    return 'system';
  }

  if (name.includes('role')) {
    return 'roles';
  }

  if (name.includes('table') || name.includes('grid')) {
    return 'table';
  }

  if (name.includes('setting') || name.includes('config')) {
    return 'settings';
  }

  if (name.includes('add') || name.includes('create') || name.includes('new')) {
    return 'plus';
  }

  if (name.includes('edit') || name.includes('update')) {
    return 'edit';
  }

  if (name.includes('delete') || name.includes('remove')) {
    return 'trash';
  }

  if (name.includes('refresh') || name.includes('reload')) {
    return 'refresh';
  }

  if (name.includes('gear') || name.includes('wrench')) {
    return 'wrench';
  }

  if (name.includes('activity') || name.includes('log')) {
    return 'activity';
  }

  return null;
}

function resolveIconName(name?: string | null): AppIconName {
  const normalized = normalizeName(name)
    .replace(/\bfill\b/g, '')
    .replace(/\bregular\b/g, '')
    .replace(/\bicon\b/g, '')
    .replace(/\s+/g, '')
    .replace(/^ph-?/, '');

  return iconRegistry[normalized as AppIconName] ? (normalized as AppIconName) : aliasMap[normalized] ?? inferIconByText(normalized) ?? 'default';
}

function resolveIcon(name?: string | null): LucideIcon {
  return iconRegistry[resolveIconName(name)];
}

export function AppIcon({
  className,
  name,
  size = 16,
  ...props
}: {
  className?: string;
  name?: string | null;
  size?: number;
} & Omit<LucideProps, 'size'>) {
  const Icon = resolveIcon(name);

  return <Icon aria-hidden className={className} size={size} strokeWidth={1.7} {...props} />;
}

export const appIcons = Object.keys(iconRegistry) as AppIconName[];
