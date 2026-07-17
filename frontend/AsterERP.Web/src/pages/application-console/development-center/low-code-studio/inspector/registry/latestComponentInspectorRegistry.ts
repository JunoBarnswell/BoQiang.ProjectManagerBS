/* eslint-disable import/order -- canonical registry order mirrors the runtime component catalog. */
import type { ComponentInspectorDefinitionBase } from '../definitions/base/ComponentInspectorDefinitionBase';
import { ActionButtonInspectorDefinition } from '../definitions/components/ActionButtonInspectorDefinition';
import { ActionImageButtonInspectorDefinition } from '../definitions/components/ActionImageButtonInspectorDefinition';
import { ActionResetButtonInspectorDefinition } from '../definitions/components/ActionResetButtonInspectorDefinition';
import { ActionSubmitButtonInspectorDefinition } from '../definitions/components/ActionSubmitButtonInspectorDefinition';
import { BusinessPrintActionInspectorDefinition } from '../definitions/components/BusinessPrintActionInspectorDefinition';
import { DocumentTemplateInspectorDefinition } from '../definitions/components/DocumentTemplateInspectorDefinition';
import { LayoutColumnInspectorDefinition } from '../definitions/components/LayoutColumnInspectorDefinition';
import { LayoutContainerInspectorDefinition } from '../definitions/components/LayoutContainerInspectorDefinition';
import { LayoutPageInspectorDefinition } from '../definitions/components/LayoutPageInspectorDefinition';
import { LayoutFormInspectorDefinition } from '../definitions/components/LayoutFormInspectorDefinition';
import { LayoutFormItemInspectorDefinition } from '../definitions/components/LayoutFormItemInspectorDefinition';
import { LayoutHtmlInspectorDefinition } from '../definitions/components/LayoutHtmlInspectorDefinition';
import { LayoutPrintInspectorDefinition } from '../definitions/components/LayoutPrintInspectorDefinition';
import { LayoutResponsiveInspectorDefinition } from '../definitions/components/LayoutResponsiveInspectorDefinition';
import { LayoutRowInspectorDefinition } from '../definitions/components/LayoutRowInspectorDefinition';
import { LayoutSplitInspectorDefinition } from '../definitions/components/LayoutSplitInspectorDefinition';
import { LayoutTableContainerInspectorDefinition } from '../definitions/components/LayoutTableContainerInspectorDefinition';
import { LayoutTabsInspectorDefinition } from '../definitions/components/LayoutTabsInspectorDefinition';
import { LayoutTemplateInspectorDefinition } from '../definitions/components/LayoutTemplateInspectorDefinition';
import { ListOlInspectorDefinition } from '../definitions/components/ListOlInspectorDefinition';
import { ListUlInspectorDefinition } from '../definitions/components/ListUlInspectorDefinition';
import { SelectCheckboxInspectorDefinition } from '../definitions/components/SelectCheckboxInspectorDefinition';
import { SemanticArticleInspectorDefinition } from '../definitions/components/SemanticArticleInspectorDefinition';
import { SemanticHeaderInspectorDefinition } from '../definitions/components/SemanticHeaderInspectorDefinition';
import { SemanticNavInspectorDefinition } from '../definitions/components/SemanticNavInspectorDefinition';
import { SemanticMainInspectorDefinition } from '../definitions/components/SemanticMainInspectorDefinition';
import { SemanticSectionInspectorDefinition } from '../definitions/components/SemanticSectionInspectorDefinition';
import { SemanticAsideInspectorDefinition } from '../definitions/components/SemanticAsideInspectorDefinition';
import { SemanticFooterInspectorDefinition } from '../definitions/components/SemanticFooterInspectorDefinition';
import { SemanticDivInspectorDefinition } from '../definitions/components/SemanticDivInspectorDefinition';
import { SemanticSpanInspectorDefinition } from '../definitions/components/SemanticSpanInspectorDefinition';
import { ListLiInspectorDefinition } from '../definitions/components/ListLiInspectorDefinition';
import { ListDlInspectorDefinition } from '../definitions/components/ListDlInspectorDefinition';
import { ListDtInspectorDefinition } from '../definitions/components/ListDtInspectorDefinition';
import { ListDdInspectorDefinition } from '../definitions/components/ListDdInspectorDefinition';
import { ListMenuInspectorDefinition } from '../definitions/components/ListMenuInspectorDefinition';
import { InteractionDetailsInspectorDefinition } from '../definitions/components/InteractionDetailsInspectorDefinition';
import { TextEmInspectorDefinition } from '../definitions/components/TextEmInspectorDefinition';
import { TextH1InspectorDefinition } from '../definitions/components/TextH1InspectorDefinition';
import { TextHeadingInspectorDefinition } from '../definitions/components/TextHeadingInspectorDefinition';
import { TextInspectorDefinition } from '../definitions/components/TextInspectorDefinition';
import { TextParagraphInspectorDefinition } from '../definitions/components/TextParagraphInspectorDefinition';
import { TextH2InspectorDefinition } from '../definitions/components/TextH2InspectorDefinition';
import { TextH3InspectorDefinition } from '../definitions/components/TextH3InspectorDefinition';
import { TextH4InspectorDefinition } from '../definitions/components/TextH4InspectorDefinition';
import { TextH5InspectorDefinition } from '../definitions/components/TextH5InspectorDefinition';
import { TextH6InspectorDefinition } from '../definitions/components/TextH6InspectorDefinition';
import { TextLinkInspectorDefinition } from '../definitions/components/TextLinkInspectorDefinition';
import { TextQuoteInspectorDefinition } from '../definitions/components/TextQuoteInspectorDefinition';
import { TextSmallInspectorDefinition } from '../definitions/components/TextSmallInspectorDefinition';
import { TextStrongInspectorDefinition } from '../definitions/components/TextStrongInspectorDefinition';
import { TextMarkInspectorDefinition } from '../definitions/components/TextMarkInspectorDefinition';
import { TextBlockquoteInspectorDefinition } from '../definitions/components/TextBlockquoteInspectorDefinition';
import { TextCodeInspectorDefinition } from '../definitions/components/TextCodeInspectorDefinition';
import { TextPreInspectorDefinition } from '../definitions/components/TextPreInspectorDefinition';
import { TextBrInspectorDefinition } from '../definitions/components/TextBrInspectorDefinition';
import { TextHrInspectorDefinition } from '../definitions/components/TextHrInspectorDefinition';
import { TextTimeInspectorDefinition } from '../definitions/components/TextTimeInspectorDefinition';
import { IntegrationApiCallInspectorDefinition } from '../definitions/components/IntegrationApiCallInspectorDefinition';
import { WorkflowActionsInspectorDefinition } from '../definitions/components/WorkflowActionsInspectorDefinition';
import { FormInputInspectorDefinition } from '../definitions/components/FormInputInspectorDefinition';
import { InputColorInspectorDefinition } from '../definitions/components/InputColorInspectorDefinition';
import { InputDateInspectorDefinition } from '../definitions/components/InputDateInspectorDefinition';
import { InputDatetimeLocalInspectorDefinition } from '../definitions/components/InputDatetimeLocalInspectorDefinition';
import { InputEmailInspectorDefinition } from '../definitions/components/InputEmailInspectorDefinition';
import { InputFileInspectorDefinition } from '../definitions/components/InputFileInspectorDefinition';
import { InputHiddenInspectorDefinition } from '../definitions/components/InputHiddenInspectorDefinition';
import { InputMonthInspectorDefinition } from '../definitions/components/InputMonthInspectorDefinition';
import { InputNumberInspectorDefinition } from '../definitions/components/InputNumberInspectorDefinition';
import { InputPasswordInspectorDefinition } from '../definitions/components/InputPasswordInspectorDefinition';
import { InputRangeInspectorDefinition } from '../definitions/components/InputRangeInspectorDefinition';
import { InputSearchInspectorDefinition } from '../definitions/components/InputSearchInspectorDefinition';
import { InputTelInspectorDefinition } from '../definitions/components/InputTelInspectorDefinition';
import { InputTextInspectorDefinition } from '../definitions/components/InputTextInspectorDefinition';
import { InputTimeInspectorDefinition } from '../definitions/components/InputTimeInspectorDefinition';
import { InputUrlInspectorDefinition } from '../definitions/components/InputUrlInspectorDefinition';
import { InputWeekInspectorDefinition } from '../definitions/components/InputWeekInspectorDefinition';
import { InputTextareaInspectorDefinition } from '../definitions/components/InputTextareaInspectorDefinition';
import { SelectDatalistInspectorDefinition } from '../definitions/components/SelectDatalistInspectorDefinition';
import { SelectDropdownInspectorDefinition } from '../definitions/components/SelectDropdownInspectorDefinition';
import { SelectMultiInspectorDefinition } from '../definitions/components/SelectMultiInspectorDefinition';
import { SelectRadioInspectorDefinition } from '../definitions/components/SelectRadioInspectorDefinition';
import { MetricMeterInspectorDefinition } from '../definitions/components/MetricMeterInspectorDefinition';
import { MetricProgressInspectorDefinition } from '../definitions/components/MetricProgressInspectorDefinition';
import { OutputValueInspectorDefinition } from '../definitions/components/OutputValueInspectorDefinition';
import { ChartBasicInspectorDefinition } from '../definitions/components/ChartBasicInspectorDefinition';
import { ReportDataTableInspectorDefinition } from '../definitions/components/ReportDataTableInspectorDefinition';
import { TableSemanticInspectorDefinition } from '../definitions/components/TableSemanticInspectorDefinition';
import { TableCaptionInspectorDefinition } from '../definitions/components/TableCaptionInspectorDefinition';
import { TableColgroupInspectorDefinition } from '../definitions/components/TableColgroupInspectorDefinition';
import { TableColInspectorDefinition } from '../definitions/components/TableColInspectorDefinition';
import { TableTheadInspectorDefinition } from '../definitions/components/TableTheadInspectorDefinition';
import { TableTbodyInspectorDefinition } from '../definitions/components/TableTbodyInspectorDefinition';
import { TableTfootInspectorDefinition } from '../definitions/components/TableTfootInspectorDefinition';
import { TableTrInspectorDefinition } from '../definitions/components/TableTrInspectorDefinition';
import { TableThInspectorDefinition } from '../definitions/components/TableThInspectorDefinition';
import { TableTdInspectorDefinition } from '../definitions/components/TableTdInspectorDefinition';
import { MediaImgInspectorDefinition } from '../definitions/components/MediaImgInspectorDefinition';
import { MediaPictureInspectorDefinition } from '../definitions/components/MediaPictureInspectorDefinition';
import { MediaSourceInspectorDefinition } from '../definitions/components/MediaSourceInspectorDefinition';
import { MediaFigureInspectorDefinition } from '../definitions/components/MediaFigureInspectorDefinition';
import { MediaFigcaptionInspectorDefinition } from '../definitions/components/MediaFigcaptionInspectorDefinition';
import { MediaAudioInspectorDefinition } from '../definitions/components/MediaAudioInspectorDefinition';
import { MediaVideoInspectorDefinition } from '../definitions/components/MediaVideoInspectorDefinition';
import { MediaTrackInspectorDefinition } from '../definitions/components/MediaTrackInspectorDefinition';
import { MediaIframeInspectorDefinition } from '../definitions/components/MediaIframeInspectorDefinition';
import { MediaCanvasInspectorDefinition } from '../definitions/components/MediaCanvasInspectorDefinition';
import { MediaSvgInspectorDefinition } from '../definitions/components/MediaSvgInspectorDefinition';
import { MediaMathInspectorDefinition } from '../definitions/components/MediaMathInspectorDefinition';
import { MediaFileUploadInspectorDefinition } from '../definitions/components/MediaFileUploadInspectorDefinition';
import { MediaImageUploadInspectorDefinition } from '../definitions/components/MediaImageUploadInspectorDefinition';
import { MediaSignatureInspectorDefinition } from '../definitions/components/MediaSignatureInspectorDefinition';
import { InteractionDialogInspectorDefinition } from '../definitions/components/InteractionDialogInspectorDefinition';
import { InteractionPopoverInspectorDefinition } from '../definitions/components/InteractionPopoverInspectorDefinition';
import { ModalDialogInspectorDefinition } from '../definitions/components/ModalDialogInspectorDefinition';
import { ModalDrawerInspectorDefinition } from '../definitions/components/ModalDrawerInspectorDefinition';

import { ComponentInspectorRegistry } from './ComponentInspectorRegistry';

export const canonicalComponentInspectorDefinitions: readonly ComponentInspectorDefinitionBase[] = [
  new LayoutPageInspectorDefinition(),
  new LayoutContainerInspectorDefinition(),
  new LayoutColumnInspectorDefinition(),
  new LayoutFormInspectorDefinition(),
  new LayoutFormItemInspectorDefinition(),
  new LayoutHtmlInspectorDefinition(),
  new LayoutPrintInspectorDefinition(),
  new LayoutResponsiveInspectorDefinition(),
  new LayoutRowInspectorDefinition(),
  new LayoutSplitInspectorDefinition(),
  new LayoutTableContainerInspectorDefinition(),
  new LayoutTabsInspectorDefinition(),
  new LayoutTemplateInspectorDefinition(),
  new DocumentTemplateInspectorDefinition(),
  new SemanticHeaderInspectorDefinition(),
  new SemanticNavInspectorDefinition(),
  new SemanticMainInspectorDefinition(),
  new SemanticSectionInspectorDefinition(),
  new SemanticArticleInspectorDefinition(),
  new SemanticAsideInspectorDefinition(),
  new SemanticFooterInspectorDefinition(),
  new SemanticDivInspectorDefinition(),
  new SemanticSpanInspectorDefinition(),
  new ListUlInspectorDefinition(),
  new ListOlInspectorDefinition(),
  new ListLiInspectorDefinition(),
  new ListDlInspectorDefinition(),
  new ListDtInspectorDefinition(),
  new ListDdInspectorDefinition(),
  new ListMenuInspectorDefinition(),
  new InteractionDetailsInspectorDefinition(),
  new TextInspectorDefinition(),
  new TextParagraphInspectorDefinition(),
  new TextHeadingInspectorDefinition(),
  new TextH1InspectorDefinition(),
  new TextH2InspectorDefinition(),
  new TextH3InspectorDefinition(),
  new TextH4InspectorDefinition(),
  new TextH5InspectorDefinition(),
  new TextH6InspectorDefinition(),
  new TextLinkInspectorDefinition(),
  new TextEmInspectorDefinition(),
  new TextStrongInspectorDefinition(),
  new TextSmallInspectorDefinition(),
  new TextMarkInspectorDefinition(),
  new TextBlockquoteInspectorDefinition(),
  new TextQuoteInspectorDefinition(),
  new TextCodeInspectorDefinition(),
  new TextPreInspectorDefinition(),
  new TextBrInspectorDefinition(),
  new TextHrInspectorDefinition(),
  new TextTimeInspectorDefinition(),
  new ActionButtonInspectorDefinition(),
  new ActionImageButtonInspectorDefinition(),
  new ActionResetButtonInspectorDefinition(),
  new ActionSubmitButtonInspectorDefinition(),
  new BusinessPrintActionInspectorDefinition(),
  new IntegrationApiCallInspectorDefinition(),
  new WorkflowActionsInspectorDefinition(),
  new FormInputInspectorDefinition(),
  new InputColorInspectorDefinition(),
  new InputDateInspectorDefinition(),
  new InputDatetimeLocalInspectorDefinition(),
  new InputEmailInspectorDefinition(),
  new InputFileInspectorDefinition(),
  new InputHiddenInspectorDefinition(),
  new InputMonthInspectorDefinition(),
  new InputNumberInspectorDefinition(),
  new InputPasswordInspectorDefinition(),
  new InputRangeInspectorDefinition(),
  new InputSearchInspectorDefinition(),
  new InputTelInspectorDefinition(),
  new InputTextInspectorDefinition(),
  new InputTimeInspectorDefinition(),
  new InputUrlInspectorDefinition(),
  new InputWeekInspectorDefinition(),
  new InputTextareaInspectorDefinition(),
  new SelectCheckboxInspectorDefinition(),
  new SelectDatalistInspectorDefinition(),
  new SelectDropdownInspectorDefinition(),
  new SelectMultiInspectorDefinition(),
  new SelectRadioInspectorDefinition(),
  new MetricMeterInspectorDefinition(),
  new MetricProgressInspectorDefinition(),
  new OutputValueInspectorDefinition(),
  new ChartBasicInspectorDefinition(),
  new ReportDataTableInspectorDefinition(),
  new TableSemanticInspectorDefinition(),
  new TableCaptionInspectorDefinition(),
  new TableColgroupInspectorDefinition(),
  new TableColInspectorDefinition(),
  new TableTheadInspectorDefinition(),
  new TableTbodyInspectorDefinition(),
  new TableTfootInspectorDefinition(),
  new TableTrInspectorDefinition(),
  new TableThInspectorDefinition(),
  new TableTdInspectorDefinition(),
  new MediaImgInspectorDefinition(),
  new MediaPictureInspectorDefinition(),
  new MediaSourceInspectorDefinition(),
  new MediaFigureInspectorDefinition(),
  new MediaFigcaptionInspectorDefinition(),
  new MediaAudioInspectorDefinition(),
  new MediaVideoInspectorDefinition(),
  new MediaTrackInspectorDefinition(),
  new MediaIframeInspectorDefinition(),
  new MediaCanvasInspectorDefinition(),
  new MediaSvgInspectorDefinition(),
  new MediaMathInspectorDefinition(),
  new MediaFileUploadInspectorDefinition(),
  new MediaImageUploadInspectorDefinition(),
  new MediaSignatureInspectorDefinition(),
  new InteractionDialogInspectorDefinition(),
  new InteractionPopoverInspectorDefinition(),
  new ModalDialogInspectorDefinition(),
  new ModalDrawerInspectorDefinition(),
];

export const latestComponentInspectorRegistry = new ComponentInspectorRegistry(canonicalComponentInspectorDefinitions);
