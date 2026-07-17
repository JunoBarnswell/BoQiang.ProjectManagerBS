namespace AsterERP.Workflow.Persistence.Entities;

public class ByteArrayRef
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public ByteArrayEntity? Entity { get; set; }
    public bool IsDeleted { get; set; }

    public ByteArrayRef() { }

    public ByteArrayRef(string id)
    {
        Id = id;
    }

    public byte[]? GetBytes()
    {
        EnsureInitialized();
        return Entity?.Bytes;
    }

    public void SetValue(string name, byte[]? bytes)
    {
        Name = name;
        SetBytes(bytes);
    }

    private void SetBytes(byte[]? bytes)
    {
        if (Id == null)
        {
            if (bytes != null)
            {
                Entity = new ByteArrayEntity
                {
                    Name = Name,
                    Bytes = bytes
                };
                Id = Entity.Id;
            }
        }
        else
        {
            EnsureInitialized();
            if (Entity != null)
            {
                Entity.Bytes = bytes;
            }
        }
    }

    public ByteArrayEntity? GetEntity()
    {
        EnsureInitialized();
        return Entity;
    }

    public void Delete()
    {
        if (!IsDeleted && Id != null)
        {
            Entity = null;
            Id = null;
            IsDeleted = true;
        }
    }

    private void EnsureInitialized()
    {
    }

    public override string ToString()
    {
        return $"ByteArrayRef[id={Id}, name={Name}{(IsDeleted ? ", deleted" : "")}]";
    }
}
