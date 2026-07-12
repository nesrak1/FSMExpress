using FSMExpress.Common.Interfaces;

// todo
namespace FSMExpress.PlayMaker.Structs;

public class FsmVariables
{
    public List<FsmFloat> FloatVariables;
    public List<FsmInt> IntVariables;
    public List<FsmBool> BoolVariables;
    public List<FsmString> StringVariables;
    public List<FsmVector2> Vector2Variables;
    public List<FsmVector3> Vector3Variables;
    public List<FsmColor> ColorVariables;
    public List<FsmRect> RectVariables;
    public List<FsmQuaternion> QuaternionVariables;
    public List<FsmGameObject> GameObjectVariables;
    public List<FsmObject> ObjectVariables;
    public List<FsmMaterial> MaterialVariables;
    public List<FsmTexture> TextureVariables;
    public List<FsmArray> ArrayVariables;
    public List<FsmEnum> EnumVariables;
    public List<string> Categories;
    public List<int> VariableCategoryIds;

    public FsmVariables()
    {
        FloatVariables = [];
        IntVariables = [];
        BoolVariables = [];
        StringVariables = [];
        Vector2Variables = [];
        Vector3Variables = [];
        ColorVariables = [];
        RectVariables = [];
        QuaternionVariables = [];
        GameObjectVariables = [];
        ObjectVariables = [];
        MaterialVariables = [];
        TextureVariables = [];
        ArrayVariables = [];
        EnumVariables = [];
        Categories = [];
        VariableCategoryIds = [];
    }

    public FsmVariables(IAssetField field)
    {
        FloatVariables = field.GetValueArray("floatVariables", x => new FsmFloat(x));
        IntVariables = field.GetValueArray("intVariables", x => new FsmInt(x));
        BoolVariables = field.GetValueArray("boolVariables", x => new FsmBool(x));
        StringVariables = field.GetValueArray("stringVariables", x => new FsmString(x));
        Vector2Variables = field.GetValueArray("vector2Variables", x => new FsmVector2(x));
        Vector3Variables = field.GetValueArray("vector3Variables", x => new FsmVector3(x));
        ColorVariables = field.GetValueArray("colorVariables", x => new FsmColor(x));
        RectVariables = field.GetValueArray("rectVariables", x => new FsmRect(x));
        QuaternionVariables = field.GetValueArray("quaternionVariables", x => new FsmQuaternion(x));
        GameObjectVariables = field.GetValueArray("gameObjectVariables", x => new FsmGameObject(x));
        ObjectVariables = field.GetValueArray("objectVariables", x => new FsmObject(x));
        MaterialVariables = field.GetValueArray("materialVariables", x => new FsmMaterial(x));
        TextureVariables = field.GetValueArray("textureVariables", x => new FsmTexture(x));
        ArrayVariables = field.GetValueArray("arrayVariables", x => new FsmArray(x));
        EnumVariables = field.GetValueArray("enumVariables", x => new FsmEnum(x));
        Categories = field.GetValue<List<string>>("categories");
        VariableCategoryIds = field.GetValue<List<int>>("variableCategoryIDs");
    }

    public bool LoadNonTemplateValues(FsmVariables repVars)
    {
        if (FloatVariables.Count < repVars.FloatVariables.Count)
            return false;
        if (IntVariables.Count < repVars.IntVariables.Count)
            return false;
        if (BoolVariables.Count < repVars.BoolVariables.Count)
            return false;
        if (StringVariables.Count < repVars.StringVariables.Count)
            return false;
        if (Vector2Variables.Count < repVars.Vector2Variables.Count)
            return false;
        if (Vector3Variables.Count < repVars.Vector3Variables.Count)
            return false;
        if (ColorVariables.Count < repVars.ColorVariables.Count)
            return false;
        if (RectVariables.Count < repVars.RectVariables.Count)
            return false;
        if (QuaternionVariables.Count < repVars.QuaternionVariables.Count)
            return false;
        if (GameObjectVariables.Count < repVars.GameObjectVariables.Count)
            return false;
        if (ObjectVariables.Count < repVars.ObjectVariables.Count)
            return false;
        if (MaterialVariables.Count < repVars.MaterialVariables.Count)
            return false;
        if (TextureVariables.Count < repVars.TextureVariables.Count)
            return false;
        if (ArrayVariables.Count < repVars.ArrayVariables.Count)
            return false;
        if (EnumVariables.Count < repVars.EnumVariables.Count)
            return false;

        for (int i = 0; i < repVars.FloatVariables.Count; i++)
        {
            if (repVars.FloatVariables[i].Name == FloatVariables[i].Name)
                FloatVariables[i] = repVars.FloatVariables[i];
        }
        for (int i = 0; i < repVars.IntVariables.Count; i++)
        {
            if (repVars.IntVariables[i].Name == IntVariables[i].Name)
                IntVariables[i] = repVars.IntVariables[i];
        }
        for (int i = 0; i < repVars.BoolVariables.Count; i++)
        {
            if (repVars.BoolVariables[i].Name == BoolVariables[i].Name)
                BoolVariables[i] = repVars.BoolVariables[i];
        }
        for (int i = 0; i < repVars.StringVariables.Count; i++)
        {
            if (repVars.StringVariables[i].Name == StringVariables[i].Name)
                StringVariables[i] = repVars.StringVariables[i];
        }
        for (int i = 0; i < repVars.Vector2Variables.Count; i++)
        {
            if (repVars.Vector2Variables[i].Name == Vector2Variables[i].Name)
                Vector2Variables[i] = repVars.Vector2Variables[i];
        }
        for (int i = 0; i < repVars.Vector3Variables.Count; i++)
        {
            if (repVars.Vector3Variables[i].Name == Vector3Variables[i].Name)
                Vector3Variables[i] = repVars.Vector3Variables[i];
        }
        for (int i = 0; i < repVars.ColorVariables.Count; i++)
        {
            if (repVars.ColorVariables[i].Name == ColorVariables[i].Name)
                ColorVariables[i] = repVars.ColorVariables[i];
        }
        for (int i = 0; i < repVars.RectVariables.Count; i++)
        {
            if (repVars.RectVariables[i].Name == RectVariables[i].Name)
                RectVariables[i] = repVars.RectVariables[i];
        }
        for (int i = 0; i < repVars.QuaternionVariables.Count; i++)
        {
            if (repVars.QuaternionVariables[i].Name == QuaternionVariables[i].Name)
                QuaternionVariables[i] = repVars.QuaternionVariables[i];
        }
        for (int i = 0; i < repVars.GameObjectVariables.Count; i++)
        {
            if (repVars.GameObjectVariables[i].Name == GameObjectVariables[i].Name)
                GameObjectVariables[i] = repVars.GameObjectVariables[i];
        }
        for (int i = 0; i < repVars.ObjectVariables.Count; i++)
        {
            if (repVars.ObjectVariables[i].Name == ObjectVariables[i].Name)
                ObjectVariables[i] = repVars.ObjectVariables[i];
        }
        for (int i = 0; i < repVars.MaterialVariables.Count; i++)
        {
            if (repVars.MaterialVariables[i].Name == MaterialVariables[i].Name)
                MaterialVariables[i] = repVars.MaterialVariables[i];
        }
        for (int i = 0; i < repVars.TextureVariables.Count; i++)
        {
            if (repVars.TextureVariables[i].Name == TextureVariables[i].Name)
                TextureVariables[i] = repVars.TextureVariables[i];
        }
        for (int i = 0; i < repVars.ArrayVariables.Count; i++)
        {
            if (repVars.ArrayVariables[i].Name == ArrayVariables[i].Name)
                ArrayVariables[i] = repVars.ArrayVariables[i];
        }
        for (int i = 0; i < repVars.EnumVariables.Count; i++)
        {
            if (repVars.EnumVariables[i].Name == EnumVariables[i].Name)
                EnumVariables[i] = repVars.EnumVariables[i];
        }

        return true;
    }
}
