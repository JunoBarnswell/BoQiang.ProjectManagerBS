import {
  AlignLeft, Type, Link, Code, Bold, Italic, MousePointerClick, ImagePlus, AlertCircle, CheckSquare, Activity, Table, List,
  CalendarDays, Clock, AtSign, FileUp, EyeOff, Hash, Lock, Search, Globe, Square, Image, Video, Music, Upload, PenTool,
  BarChart, Printer, ArrowRightSquare, MessageSquare, Layout, FormInput, AppWindow, Box, Columns, Rows, LayoutGrid, MonitorPlay,
  LayoutTemplate
} from 'lucide-react';

export function getComponentIcon(type: string, acceptsChildren: boolean) {
  const className = "h-4 w-4 stroke-[1.5px]";

  // exact matches
  switch (type) {
    case 'layout.page': return <AppWindow className={className} />;
    case 'layout.container': return <Box className={className} />;
    case 'layout.column': return <Columns className={className} />;
    case 'layout.row': return <Rows className={className} />;
    case 'layout.split': return <LayoutGrid className={className} />;
    case 'layout.form': return <FormInput className={className} />;
    case 'text.paragraph': return <AlignLeft className={className} />;
    case 'text.heading': case 'text.h1': case 'text.h2': case 'text.h3': case 'text.h4': case 'text.h5': case 'text.h6': return <Type className={className} />;
    case 'text.link': return <Link className={className} />;
    case 'text.code': case 'text.pre': return <Code className={className} />;
    case 'text.strong': return <Bold className={className} />;
    case 'text.em': return <Italic className={className} />;
    case 'action.button': return <MousePointerClick className={className} />;
    case 'action.imageButton': return <ImagePlus className={className} />;
    case 'action.resetButton': return <AlertCircle className={className} />;
    case 'action.submitButton': return <CheckSquare className={className} />;
    case 'metric.meter': case 'metric.progress': return <Activity className={className} />;
    case 'report.dataTable': case 'table.semantic': return <Table className={className} />;
    case 'select.checkbox': case 'select.multi': return <CheckSquare className={className} />;
    case 'select.radio': case 'select.dropdown': case 'select.datalist': return <List className={className} />;
    case 'input.date': return <CalendarDays className={className} />;
    case 'input.datetimeLocal': case 'input.time': return <Clock className={className} />;
    case 'input.email': return <AtSign className={className} />;
    case 'input.file': return <FileUp className={className} />;
    case 'input.hidden': return <EyeOff className={className} />;
    case 'input.number': case 'input.range': return <Hash className={className} />;
    case 'input.password': return <Lock className={className} />;
    case 'input.search': return <Search className={className} />;
    case 'input.tel': return <Hash className={className} />;
    case 'input.text': case 'input.textarea': return <Type className={className} />;
    case 'input.url': return <Globe className={className} />;
    case 'input.color': return <Square className={className} />;
    case 'media.img': case 'media.picture': return <Image className={className} />;
    case 'media.video': return <Video className={className} />;
    case 'media.audio': return <Music className={className} />;
    case 'media.fileUpload': return <Upload className={className} />;
    case 'media.imageUpload': return <ImagePlus className={className} />;
    case 'media.signature': return <PenTool className={className} />;
    case 'chart.basic': return <BarChart className={className} />;
    case 'business.printAction': return <Printer className={className} />;
    case 'workflow.actions': return <ArrowRightSquare className={className} />;
    case 'interaction.dialog': case 'interaction.popover': return <MessageSquare className={className} />;
  }

  // category fallback
  const category = type.split('.')[0];
  switch (category) {
    case 'layout': return <Layout className={className} />;
    case 'text': return <Type className={className} />;
    case 'action': return <MousePointerClick className={className} />;
    case 'input': return <FormInput className={className} />;
    case 'select': return <List className={className} />;
    case 'media': return <MonitorPlay className={className} />;
    case 'table': return <Table className={className} />;
    case 'semantic': return <LayoutTemplate className={className} />;
    case 'list': return <List className={className} />;
  }

  // absolute fallback
  return acceptsChildren ? <LayoutGrid className={className} /> : <Box className={className} />;
}
