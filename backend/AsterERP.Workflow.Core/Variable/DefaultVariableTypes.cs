namespace AsterERP.Workflow.Core.Variable;

public class DefaultVariableTypes : VariableTypes
{
    public DefaultVariableTypes()
    {
        AddType(new NullType());
        AddType(new JsonType());
        AddType(new LongJsonType());
        AddType(new StringType());
        AddType(new LongStringType());
        AddType(new BooleanType());
        AddType(new ShortType());
        AddType(new IntegerType());
        AddType(new LongType());
        AddType(new DoubleType());
        AddType(new BigDecimalType());
        AddType(new DateType());
        AddType(new LocalDateType());
        AddType(new LocalDateTimeType());
        AddType(new UUIDType());
        AddType(new ByteArrayType());
        AddType(new CustomObjectType());
        AddType(new SerializableType());
    }
}
