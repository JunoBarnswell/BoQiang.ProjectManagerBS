namespace AsterERP.Workflow.BpmnParser.Converter;

public static class BpmnXMLConstants
{
    public const string BPMN2_NAMESPACE = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    public const string BPMN2_PREFIX = "bpmn";
    public const string BPMNDI_NAMESPACE = "http://www.omg.org/spec/BPMN/20100524/DI";
    public const string BPMNDI_PREFIX = "bpmndi";
    public const string OMGDC_NAMESPACE = "http://www.omg.org/spec/DD/20100524/DC";
    public const string OMGDC_PREFIX = "omgdc";
    public const string OMGDI_NAMESPACE = "http://www.omg.org/spec/DD/20100524/DI";
    public const string OMGDI_PREFIX = "omgdi";
    public const string WORKFLOW_EXTENSION_NAMESPACE = "http://AsterERP.Workflow.org/bpmn";
    public const string WORKFLOW_EXTENSION_PREFIX = "activiti";
    public const string XSI_NAMESPACE = "http://www.w3.org/2001/XMLSchema-instance";
    public const string XSI_PREFIX = "xsi";
    public const string XSD_NAMESPACE = "http://www.w3.org/2001/XMLSchema";
    public const string XSD_PREFIX = "xsd";
    public const string SCHEMA_NAMESPACE = "http://www.w3.org/2001/XMLSchema";
    public const string XPATH_NAMESPACE = "http://www.w3.org/1999/XPath";
    public const string PROCESS_NAMESPACE = "http://AsterERP.Workflow.org/bpmn";

    public const string ATTRIBUTE_ID = "id";
    public const string ATTRIBUTE_NAME = "name";
    public const string ATTRIBUTE_DEFAULT = "default";
    public const string ATTRIBUTE_PROCESS_EXECUTABLE = "isExecutable";
    public const string ATTRIBUTE_PROCESS_CANDIDATE_USERS = "candidateStarterUsers";
    public const string ATTRIBUTE_PROCESS_CANDIDATE_GROUPS = "candidateStarterGroups";
    public const string ATTRIBUTE_PROCESS_REF = "processRef";
    public const string ATTRIBUTE_ACTIVITY_ASYNCHRONOUS = "async";
    public const string ATTRIBUTE_ACTIVITY_EXCLUSIVE = "exclusive";
    public const string ATTRIBUTE_ACTIVITY_ISFORCOMPENSATION = "isForCompensation";
    public const string ATTRIBUTE_TRIGGERED_BY = "triggeredByEvent";
    public const string ATTRIBUTE_CANCEL_REMAINING_INSTANCES = "cancelRemainingInstances";
    public const string ATTRIBUTE_ORDERING = "ordering";
    public const string ATTRIBUTE_VALUE_TRUE = "true";
    public const string ATTRIBUTE_VALUE_FALSE = "false";

    public const string ATTRIBUTE_SIGNAL_REF = "signalRef";
    public const string ATTRIBUTE_MESSAGE_REF = "messageRef";
    public const string ATTRIBUTE_ERROR_REF = "errorRef";
    public const string ATTRIBUTE_ERROR_CODE = "errorCode";
    public const string ATTRIBUTE_SCOPE = "scope";
    public const string ATTRIBUTE_ITEM_REF = "itemRef";
    public const string ATTRIBUTE_ITEM_SUBJECT_REF = "itemSubjectRef";
    public const string ATTRIBUTE_MESSAGE_CORRELATION_KEY = "correlationKey";
    public const string ATTRIBUTE_MESSAGE_EXPRESSION = "messageExpression";
    public const string ATTRIBUTE_CALENDAR_NAME = "calendarName";
    public const string ATTRIBUTE_END_DATE = "endDate";
    public const string ATTRIBUTE_COMPENSATE_ACTIVITYREF = "activityRef";
    public const string ATTRIBUTE_TERMINATE_ALL = "terminateAll";
    public const string ATTRIBUTE_TERMINATE_MULTI_INSTANCE = "terminateMultiInstance";
    public const string ATTRIBUTE_ESCALATION_REF = "escalationRef";
    public const string ATTRIBUTE_ESCALATION_CODE = "escalationCode";

    public const string ATTRIBUTE_FORM_ID = "id";
    public const string ATTRIBUTE_FORM_NAME = "name";
    public const string ATTRIBUTE_FORM_TYPE = "type";
    public const string ATTRIBUTE_FORM_EXPRESSION = "expression";
    public const string ATTRIBUTE_FORM_VARIABLE = "variable";
    public const string ATTRIBUTE_FORM_DEFAULT = "default";
    public const string ATTRIBUTE_FORM_DATEPATTERN = "datePattern";
    public const string ATTRIBUTE_FORM_READABLE = "readable";
    public const string ATTRIBUTE_FORM_WRITABLE = "writable";
    public const string ATTRIBUTE_FORM_REQUIRED = "required";

    public const string ATTRIBUTE_FIELD_NAME = "fieldName";
    public const string ATTRIBUTE_FIELD_EXPRESSION = "expression";
    public const string ATTRIBUTE_LISTENER_EVENT = "event";
    public const string ATTRIBUTE_LISTENER_CLASS = "class";
    public const string ATTRIBUTE_LISTENER_EXPRESSION = "expression";
    public const string ATTRIBUTE_LISTENER_DELEGATEEXPRESSION = "delegateExpression";
    public const string ATTRIBUTE_LISTENER_ON_TRANSACTION = "onTransaction";
    public const string ATTRIBUTE_LISTENER_EVENTS = "events";
    public const string ATTRIBUTE_LISTENER_ENTITY_TYPE = "entityType";
    public const string ATTRIBUTE_LISTENER_THROW_SIGNAL_EVENT_NAME = "signalName";
    public const string ATTRIBUTE_LISTENER_THROW_EVENT_TYPE = "eventType";
    public const string ATTRIBUTE_LISTENER_THROW_EVENT_TYPE_SIGNAL = "signal";
    public const string ATTRIBUTE_LISTENER_THROW_EVENT_TYPE_GLOBAL_SIGNAL = "globalSignal";
    public const string ATTRIBUTE_LISTENER_THROW_MESSAGE_EVENT_NAME = "messageName";
    public const string ATTRIBUTE_LISTENER_THROW_EVENT_TYPE_MESSAGE = "message";
    public const string ATTRIBUTE_LISTENER_THROW_ERROR_EVENT_CODE = "errorCode";
    public const string ATTRIBUTE_LISTENER_THROW_EVENT_TYPE_ERROR = "error";
    public const string ATTRIBUTE_LISTENER_CUSTOM_PROPERTIES_RESOLVER_CLASS = "customPropertiesResolverClass";
    public const string ATTRIBUTE_LISTENER_CUSTOM_PROPERTIES_RESOLVER_EXPRESSION = "customPropertiesResolverExpression";
    public const string ATTRIBUTE_LISTENER_CUSTOM_PROPERTIES_RESOLVER_DELEGATEEXPRESSION = "customPropertiesResolverDelegateExpression";

    public const string ATTRIBUTE_MULTIINSTANCE_SEQUENTIAL = "isSequential";
    public const string ATTRIBUTE_MULTIINSTANCE_COLLECTION = "collection";
    public const string ATTRIBUTE_MULTIINSTANCE_VARIABLE = "elementVariable";
    public const string ATTRIBUTE_MULTIINSTANCE_INDEX_VARIABLE = "elementIndexVariable";

    public const string ATTRIBUTE_FLOW_SOURCE_REF = "sourceRef";
    public const string ATTRIBUTE_FLOW_TARGET_REF = "targetRef";
    public const string ATTRIBUTE_DI_BPMNELEMENT = "bpmnElement";
    public const string ATTRIBUTE_DI_HEIGHT = "height";
    public const string ATTRIBUTE_DI_WIDTH = "width";
    public const string ATTRIBUTE_DI_X = "x";
    public const string ATTRIBUTE_DI_Y = "y";
    public const string ATTRIBUTE_DI_IS_EXPANDED = "isExpanded";

    public const string TYPE_LANGUAGE_ATTRIBUTE = "typeLanguage";
    public const string EXPRESSION_LANGUAGE_ATTRIBUTE = "expressionLanguage";
    public const string TARGET_NAMESPACE_ATTRIBUTE = "targetNamespace";

    public const string ELEMENT_DEFINITIONS = "definitions";
    public const string ELEMENT_PROCESS = "process";
    public const string ELEMENT_SUBPROCESS = "subProcess";
    public const string ELEMENT_TRANSACTION = "transaction";
    public const string ELEMENT_ADHOC_SUBPROCESS = "adHocSubProcess";
    public const string ELEMENT_DOCUMENTATION = "documentation";
    public const string ELEMENT_EXTENSIONS = "extensionElements";
    public const string ELEMENT_SIGNAL = "signal";
    public const string ELEMENT_MESSAGE = "message";
    public const string ELEMENT_ERROR = "error";
    public const string ELEMENT_IMPORT = "import";
    public const string ELEMENT_ITEM_DEFINITION = "itemDefinition";
    public const string ELEMENT_DATA_STORE = "dataStore";
    public const string ELEMENT_INTERFACE = "interface";
    public const string ELEMENT_IOSPECIFICATION = "ioSpecification";
    public const string ELEMENT_PARTICIPANT = "participant";
    public const string ELEMENT_MESSAGE_FLOW = "messageFlow";
    public const string ELEMENT_POTENTIAL_STARTER = "potentialStarter";
    public const string ELEMENT_LANE = "lane";
    public const string ELEMENT_LANESET = "laneSet";
    public const string ELEMENT_RESOURCE = "resource";
    public const string ELEMENT_COMPLETION_CONDITION = "completionCondition";
    public const string ELEMENT_COLLABORATION = "collaboration";

    public const string ELEMENT_EVENT_TIMERDEFINITION = "timerEventDefinition";
    public const string ELEMENT_EVENT_SIGNALDEFINITION = "signalEventDefinition";
    public const string ELEMENT_EVENT_MESSAGEDEFINITION = "messageEventDefinition";
    public const string ELEMENT_EVENT_ERRORDEFINITION = "errorEventDefinition";
    public const string ELEMENT_EVENT_CANCELDEFINITION = "cancelEventDefinition";
    public const string ELEMENT_EVENT_COMPENSATEDEFINITION = "compensateEventDefinition";
    public const string ELEMENT_EVENT_TERMINATEDEFINITION = "terminateEventDefinition";
    public const string ELEMENT_EVENT_LINKDEFINITION = "linkEventDefinition";
    public const string ELEMENT_EVENT_ESCALATIONDEFINITION = "escalationEventDefinition";
    public const string ELEMENT_EVENT_CONDITIONALDEFINITION = "conditionalEventDefinition";
    public const string ELEMENT_EVENT_CONDITION = "condition";

    public const string ATTRIBUTE_TIMER_DATE = "timeDate";
    public const string ATTRIBUTE_TIMER_CYCLE = "timeCycle";
    public const string ATTRIBUTE_TIMER_DURATION = "timeDuration";

    public const string ELEMENT_MULTIINSTANCE = "multiInstanceLoopCharacteristics";
    public const string ELEMENT_MULTIINSTANCE_CARDINALITY = "loopCardinality";
    public const string ELEMENT_MULTIINSTANCE_CONDITION = "completionCondition";
    public const string ELEMENT_MULTI_INSTANCE_DATA_OUTPUT = "loopDataOutputRef";
    public const string ELEMENT_MULTI_INSTANCE_OUTPUT_DATA_ITEM = "outputDataItem";
    public const string ELEMENT_MULTI_INSTANCE_DATA_INPUT = "dataInput";
    public const string ELEMENT_MULTI_INSTANCE_INPUT_DATA_ITEM = "inputDataItem";

    public const string ELEMENT_FORMPROPERTY = "formProperty";
    public const string ELEMENT_VALUE = "value";
    public const string ELEMENT_FIELD = "field";
    public const string ELEMENT_FIELD_STRING = "string";
    public const string ELEMENT_EXECUTION_LISTENER = "executionListener";
    public const string ELEMENT_TASK_LISTENER = "taskListener";
    public const string ELEMENT_EVENT_LISTENER = "eventListener";
    public const string FAILED_JOB_RETRY_TIME_CYCLE = "failedJobRetryTimeCycle";

    public const string ELEMENT_DI_DIAGRAM = "BPMNDiagram";
    public const string ELEMENT_DI_PLANE = "BPMNPlane";
    public const string ELEMENT_DI_SHAPE = "BPMNShape";
    public const string ELEMENT_DI_EDGE = "BPMNEdge";
    public const string ELEMENT_DI_BOUNDS = "Bounds";
    public const string ELEMENT_DI_WAYPOINT = "waypoint";
    public const string ELEMENT_DI_LABEL = "BPMNLabel";
    public const string ELEMENT_FLOWNODE_REF = "flowNodeRef";
    public const string ELEMENT_DATA_STATE = "dataState";
    public const string ELEMENT_DATA_INPUT_ASSOCIATION = "dataInputAssociation";
    public const string ELEMENT_DATA_OUTPUT_ASSOCIATION = "dataOutputAssociation";

    public const string ELEMENT_GATEWAY_INCOMING = "incoming";
    public const string ELEMENT_GATEWAY_OUTGOING = "outgoing";
}
