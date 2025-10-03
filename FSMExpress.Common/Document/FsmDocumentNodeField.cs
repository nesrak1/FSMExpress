using AssetsTools.NET;

namespace FSMExpress.Common.Document;
public abstract class FsmDocumentNodeField { }

public class FsmDocumentNodeClassField(AssetTypeReference typeRef, bool isEnabled) : FsmDocumentNodeField
{
    public AssetTypeReference TypeRef { get; set; } = typeRef;
    public bool IsEnabled { get; set; } = isEnabled;
}

public class FsmDocumentNodeIndexedClassField(FsmDocumentNodeClassField field, int index) : FsmDocumentNodeClassField(field.TypeRef, field.IsEnabled)
{
    public int Index { get; set; } = index;
    public string Name => $"({Index}) {TypeRef.ClassName}";
}

public class FsmDocumentNodeDataField(string key, FsmDocumentNodeFieldValue value) : FsmDocumentNodeField
{
    public string Key { get; set; } = key;
    public FsmDocumentNodeFieldValue Value { get; set; } = value;
}