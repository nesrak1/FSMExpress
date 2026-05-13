using AssetsTools.NET;
using FSMExpress.Common.Document;
using FSMExpress.Common.Interfaces;
using FSMExpress.PlayMaker.Structs;
using System.Drawing;
using System.Text;
using DrawingColor = System.Drawing.Color;
using EngineColor = FSMExpress.PlayMaker.Structs.Color;

namespace FSMExpress.PlayMaker;

public class FsmPlaymaker : IFsmMonoBehaviour
{
    public int Version { get; set; } = -1;
    public string Name { get; set; } = "";
    public string GoName { get; set; } = "";

    public FsmState? StartState = null;
    public List<FsmState> States { get; } = [];
    public List<FsmEvent> Events { get; } = [];
    public List<FsmTransition> GlobalTransitions { get; } = [];
    public FsmVariables Variables { get; set; }

    // assetstools has no way of getting us the assembly/namespace
    // through its api, so we'll just assume this is always the
    // same namespace/assembly and hardcode it.
    const string PM_NAMESPACE = "HutongGames.PlayMaker";
    const string PM_ASSEMBLY = "PlayMaker.dll";

    public FsmPlaymaker(IAssetField field)
    {
        Version = field.GetValue<int>("dataVersion");
        Name = field.GetValue<string>("name");
        GoName = string.Empty; // needs filling from outside of constructor

        var startStateName = field.GetValue<string>("startState");
        States = field.GetValueArray("states", x => new FsmState(x));
        Events = field.GetValueArray("events", x => new FsmEvent(x));
        GlobalTransitions = field.GetValueArray("globalTransitions", x => new FsmTransition(x));
        Variables = new FsmVariables(field.GetField("variables"));
        StartState = States.Find(s => s.Name == startStateName);
    }

    public FsmDocument MakeDocument()
    {
        var stateLookup = new Dictionary<string, FsmDocumentNode>();
        var doc = new FsmDocument(Name, GoName);
        foreach (var state in States)
        {
            var docNode = new FsmDocumentNode(state.Name);
            doc.Nodes.Add(docNode);
            stateLookup[state.Name] = docNode;
        }

        AddStatesToDoc(stateLookup);
        AddTransitionsToDoc(doc, stateLookup);
        AddEventsToDoc(doc);
        AddVariablesToDoc(doc);

        return doc;
    }

    private void AddStatesToDoc(Dictionary<string, FsmDocumentNode> stateLookup)
    {
        foreach (var state in States)
        {
            var docNode = stateLookup[state.Name];
            docNode.IsStart = state == StartState;

            var statePos = state.Position;
            docNode.Bounds = new RectangleF(statePos.X, statePos.Y, statePos.Width, statePos.Height);

            var stateColor = state.ColorIndex;
            if (stateColor >= STATE_COLORS.Length)
                stateColor = (byte)(STATE_COLORS.Length - 1); // todo: fix new colors!

            docNode.NodeColor = STATE_COLORS[stateColor];
            docNode.TransitionColor = TRANSITION_COLORS[stateColor];

            foreach (var transition in state.Transitions)
            {
                var docNodeTransition = new FsmDocumentNodeTransition(transition.Event.Name);
                if (stateLookup.TryGetValue(transition.ToState, out var toNode))
                {
                    docNodeTransition.ToNode = toNode;
                }

                docNode.Transitions.Add(docNodeTransition);
            }

            var stateActionData = state.ActionData;
            for (var actionIdx = 0; actionIdx < stateActionData.ActionNames.Count; actionIdx++)
            {
                var actionName = TrimFullNameToClassName(stateActionData.ActionNames[actionIdx]);
                var actionEnabled = stateActionData.ActionEnabled[actionIdx] != 0;

                docNode.Fields.Add(new FsmDocumentNodeClassField(new AssetTypeReference(actionName, "Namespace", "AssemblyName"), actionEnabled));
                ConvertActionData(docNode.Fields, stateActionData, actionIdx);
            }
        }
    }

    private void AddTransitionsToDoc(FsmDocument doc, Dictionary<string, FsmDocumentNode> stateLookup)
    {
        foreach (var transition in GlobalTransitions)
        {
            var toNode = stateLookup[transition.ToState];
            var docNode = new FsmDocumentNode(transition.Event.Name)
            {
                IsStart = false,
                IsGlobal = true,
                Bounds = new RectangleF(toNode.Bounds.X, toNode.Bounds.Y - 50, toNode.Bounds.Width, 18)
            };

            var docNodeTransition = new FsmDocumentNodeTransition(transition.Event.Name)
            {
                ToNode = toNode
            };

            docNode.Transitions.Add(docNodeTransition);
            doc.Nodes.Add(docNode);
        }
    }

    private void AddEventsToDoc(FsmDocument doc)
    {
        foreach (var evt in Events)
        {
            var docEvt = new FsmDocumentEvent(evt.Name)
            {
                IsSystem = evt.IsSystemEvent,
                IsGlobal = evt.IsGlobal
            };

            doc.Events.Add(docEvt);
        }
    }

    private void AddVariablesToDoc(FsmDocument doc)
    {
        if (Variables.FloatVariables.Count > 0)
        {
            AddVariableClassToDoc(doc, "FsmFloat", PM_NAMESPACE, PM_ASSEMBLY);
            foreach (var variable in Variables.FloatVariables)
            {
                var docVarVal = new FsmDocumentNodeFieldFloatValue(variable.Value, string.Empty);
                var docVar = new FsmDocumentNodeDataField(variable.Name, docVarVal);
                doc.Variables.Add(docVar);
            }
        }

        if (Variables.IntVariables.Count > 0)
        {
            AddVariableClassToDoc(doc, "FsmInt", PM_NAMESPACE, PM_ASSEMBLY);
            foreach (var variable in Variables.IntVariables)
            {
                var docVarVal = new FsmDocumentNodeFieldIntegerValue(variable.Value, string.Empty);
                var docVar = new FsmDocumentNodeDataField(variable.Name, docVarVal);
                doc.Variables.Add(docVar);
            }
        }

        if (Variables.BoolVariables.Count > 0)
        {
            AddVariableClassToDoc(doc, "FsmBool", PM_NAMESPACE, PM_ASSEMBLY);
            foreach (var variable in Variables.BoolVariables)
            {
                var docVarVal = new FsmDocumentNodeFieldBooleanValue(variable.Value, string.Empty);
                var docVar = new FsmDocumentNodeDataField(variable.Name, docVarVal);
                doc.Variables.Add(docVar);
            }
        }

        if (Variables.StringVariables.Count > 0)
        {
            AddVariableClassToDoc(doc, "FsmString", PM_NAMESPACE, PM_ASSEMBLY);
            foreach (var variable in Variables.StringVariables)
            {
                var docVarVal = new FsmDocumentNodeFieldStringValue(variable.Value, string.Empty);
                var docVar = new FsmDocumentNodeDataField(variable.Name, docVarVal);
                doc.Variables.Add(docVar);
            }
        }

        AddVariableToDoc(doc, "FsmVector2", Variables.Vector2Variables);
        AddVariableToDoc(doc, "FsmVector3", Variables.Vector3Variables);
        AddVariableToDoc(doc, "FsmColor", Variables.ColorVariables);
        AddVariableToDoc(doc, "FsmRect", Variables.RectVariables);
        AddVariableToDoc(doc, "FsmQuaternion", Variables.QuaternionVariables);
        AddVariableToDoc(doc, "FsmGameObject", Variables.GameObjectVariables);
        AddVariableToDoc(doc, "FsmObject", Variables.ObjectVariables);
        AddVariableToDoc(doc, "FsmMaterial", Variables.MaterialVariables);
        AddVariableToDoc(doc, "FsmTexture", Variables.TextureVariables);
        AddArrayVariableToDoc(doc, "FsmArray", Variables.ArrayVariables);
        AddVariableToDoc(doc, "FsmEnum", Variables.EnumVariables);
    }

    private void AddVariableToDoc<T>(FsmDocument doc, string name, List<T> values)
        where T : NamedVariable, IFsmPlaymakerValuePreviewer
    {
        if (values.Count > 0)
        {
            AddVariableClassToDoc(doc, name, PM_NAMESPACE, PM_ASSEMBLY);
            foreach (var variable in values)
            {
                var docVarVal = new FsmPlaymakerValue(variable, string.Empty, 0);
                var docVar = new FsmDocumentNodeDataField(variable.Name, docVarVal);
                doc.Variables.Add(docVar);
            }
        }
    }

    private void AddVariableClassToDoc(FsmDocument doc, string className, string nameSpace, string asmName)
    {
        var reference = new AssetTypeReference(className, nameSpace, asmName);
        doc.Variables.Add(new FsmDocumentNodeClassField(reference, true));
    }

    private void AddArrayVariableToDoc(FsmDocument doc, string name, List<FsmArray> values)
    {
        if (values.Count > 0)
        {
            AddVariableClassToDoc(doc, name, PM_NAMESPACE, PM_ASSEMBLY);
            foreach (var variable in values)
            {
                // arrays are not supported, so we ignore them
                var typeName = variable.VarType switch
                {
                    VariableType.Float => "float",
                    VariableType.Int => "int",
                    VariableType.Bool => "bool",
                    VariableType.GameObject => "GameObject",
                    VariableType.String => "string",
                    VariableType.Vector2 => "Vector2",
                    VariableType.Vector3 => "Vector3",
                    VariableType.Color => "Color",
                    VariableType.Rect => "Rect",
                    VariableType.Material => "Material",
                    VariableType.Texture => "Texture",
                    VariableType.Quaternion => "Quaternion",
                    VariableType.Object => variable.ObjectTypeName,
                    VariableType.Enum => variable.ObjectTypeName, // could potentially be typeof(SomeEnum).FullName
                    _ => "Unsupported"
                };

                var itemCount = variable.VarType switch
                {
                    VariableType.Float => variable.FloatValues.Count,
                    VariableType.Int => variable.IntValues.Count,
                    VariableType.Bool => variable.BoolValues.Count,
                    VariableType.GameObject => variable.ObjectReferences.Count,
                    VariableType.String => variable.StringValues.Count,
                    VariableType.Vector2 => variable.Vector4Values.Count,
                    VariableType.Vector3 => variable.Vector4Values.Count,
                    VariableType.Color => variable.Vector4Values.Count,
                    VariableType.Rect => variable.Vector4Values.Count,
                    VariableType.Material => variable.ObjectReferences.Count,
                    VariableType.Texture => variable.ObjectReferences.Count,
                    VariableType.Quaternion => variable.Vector4Values.Count,
                    VariableType.Object => variable.ObjectReferences.Count,
                    VariableType.Enum => variable.IntValues.Count,
                    _ => 0
                };

                var docVarVal = new FsmDocumentNodeFieldArrayValue(typeName, string.Empty, itemCount);
                var docVar = new FsmDocumentNodeDataField(variable.Name, docVarVal);
                doc.Variables.Add(docVar);

                switch (variable.VarType)
                {
                    case VariableType.Float:
                    {
                        for (var eleIdx = 0; eleIdx < variable.FloatValues.Count; eleIdx++)
                        {
                            var eleVariable = variable.FloatValues[eleIdx];
                            var docEleVarVal = new FsmDocumentNodeFieldFloatValue(eleVariable, string.Empty, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.Int:
                    case VariableType.Enum:
                    {
                        for (var eleIdx = 0; eleIdx < variable.IntValues.Count; eleIdx++)
                        {
                            var eleVariable = variable.IntValues[eleIdx];
                            var docEleVarVal = new FsmDocumentNodeFieldIntegerValue(eleVariable, string.Empty, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.GameObject:
                    case VariableType.Material:
                    case VariableType.Texture:
                    case VariableType.Object:
                    {
                        for (var eleIdx = 0; eleIdx < variable.ObjectReferences.Count; eleIdx++)
                        {
                            var eleVariable = variable.ObjectReferences[eleIdx];
                            var docEleVarVal = new FsmDocumentNodeFieldFallbackValue(eleVariable, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.String:
                    {
                        for (var eleIdx = 0; eleIdx < variable.StringValues.Count; eleIdx++)
                        {
                            var eleVariable = variable.StringValues[eleIdx];
                            var docEleVarVal = new FsmDocumentNodeFieldStringValue(eleVariable, string.Empty, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.Vector2:
                    {
                        for (var eleIdx = 0; eleIdx < variable.Vector4Values.Count; eleIdx++)
                        {
                            var eleRawVariable = variable.Vector4Values[eleIdx];
                            var eleVariable = new Vector2 { X = eleRawVariable.X, Y = eleRawVariable.Y };
                            var docEleVarVal = new FsmDocumentNodeFieldFallbackValue(eleVariable, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.Vector3:
                    {
                        for (var eleIdx = 0; eleIdx < variable.Vector4Values.Count; eleIdx++)
                        {
                            var eleRawVariable = variable.Vector4Values[eleIdx];
                            var eleVariable = new Vector3 { X = eleRawVariable.X, Y = eleRawVariable.Y, Z = eleRawVariable.Z };
                            var docEleVarVal = new FsmDocumentNodeFieldFallbackValue(eleVariable, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.Color:
                    {
                        for (var eleIdx = 0; eleIdx < variable.Vector4Values.Count; eleIdx++)
                        {
                            var eleRawVariable = variable.Vector4Values[eleIdx];
                            var eleVariable = new EngineColor { R = eleRawVariable.X, G = eleRawVariable.Y, B = eleRawVariable.Z, A = eleRawVariable.W };
                            var docEleVarVal = new FsmDocumentNodeFieldFallbackValue(eleVariable, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.Rect:
                    {
                        for (var eleIdx = 0; eleIdx < variable.Vector4Values.Count; eleIdx++)
                        {
                            var eleRawVariable = variable.Vector4Values[eleIdx];
                            var eleVariable = new Rect { X = eleRawVariable.X, Y = eleRawVariable.Y, Width = eleRawVariable.Z, Height = eleRawVariable.W };
                            var docEleVarVal = new FsmDocumentNodeFieldFallbackValue(eleVariable, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                    case VariableType.Quaternion:
                    {
                        for (var eleIdx = 0; eleIdx < variable.Vector4Values.Count; eleIdx++)
                        {
                            var eleRawVariable = variable.Vector4Values[eleIdx];
                            var eleVariable = new Quaternion { X = eleRawVariable.X, Y = eleRawVariable.Y, Z = eleRawVariable.Z, W = eleRawVariable.W };
                            var docEleVarVal = new FsmDocumentNodeFieldFallbackValue(eleVariable, 1);
                            var docEleVar = new FsmDocumentNodeDataField($"{variable.Name}[{eleIdx}]", docEleVarVal);
                            doc.Variables.Add(docEleVar);
                        }
                        break;
                    }
                }
            }
        }
    }

    private FsmArrayInfo ConvertActionDataArray(FsmActionData actionData, ref int paramIdx)
    {
        var type = actionData.ArrayParamTypes[actionData.ParamDataPos[paramIdx]];
        var size = actionData.ArrayParamSizes[actionData.ParamDataPos[paramIdx]];

        var elements = new object[size];
        for (var eleIdx = 0; eleIdx < size; eleIdx++)
        {
            paramIdx++;
            elements[eleIdx] = ConvertFsmObject(actionData, ref paramIdx);
        }

        return new FsmArrayInfo(TrimFullNameToClassName(type), elements);
    }

    private void ConvertActionData(List<FsmDocumentNodeField> fields, FsmActionData actionData, int actionIdx)
    {
        var startIndex = actionData.ActionStartIndex[actionIdx];
        var endIndex = (actionIdx == actionData.ActionNames.Count - 1)
            ? actionData.ParamDataType.Count
            : actionData.ActionStartIndex[actionIdx + 1];

        for (var paramIdx = startIndex; paramIdx < endIndex; paramIdx++)
        {
            if (paramIdx < actionData.ParamName.Count)
            {
                var paramName = actionData.ParamName[paramIdx];
                var paramObj = ConvertFsmObject(actionData, ref paramIdx);
                fields.Add(new FsmDocumentNodeDataField(paramName, ConvertObjectToNodeFieldValue(paramObj, false)));
                if (paramObj is FsmArrayInfo objArrayInf)
                {
                    for (var eleIdx = 0; eleIdx < objArrayInf.Elements.Length; eleIdx++)
                    {
                        var element = objArrayInf.Elements[eleIdx];
                        fields.Add(new FsmDocumentNodeDataField($"{paramName}[{eleIdx}]", ConvertObjectToNodeFieldValue(element, true)));
                    }
                }
            }
            else
            {
                fields.Add(new FsmDocumentNodeDataField("invalid", ConvertObjectToNodeFieldValue("[out of bounds field]", false)));
            }
        }
    }

    private object ConvertFsmObject(FsmActionData actionData, ref int paramIdx)
    {
        var paramDataType = actionData.ParamDataType[paramIdx];
        var paramDataPos = actionData.ParamDataPos[paramIdx];
        var paramByteDataSize = actionData.ParamByteDataSize[paramIdx];
        var paramName = actionData.ParamName[paramIdx];

        var r = new BinaryReader(new MemoryStream(actionData.ByteData));
        r.BaseStream.Position = paramDataPos;

        object ret;
        try
        {
            ret = paramDataType switch
            {
                ParamDataType.Integer => r.ReadInt32(),
                ParamDataType.FsmInt when Version == 1 => new FsmInt { Value = r.ReadInt32() },
                ParamDataType.Enum => r.ReadInt32(),
                ParamDataType.Boolean => r.ReadBoolean(),
                ParamDataType.FsmBool when Version == 1 => new FsmBool { Value = r.ReadBoolean() },
                ParamDataType.Float => r.ReadSingle(),
                ParamDataType.FsmFloat when Version == 1 => new FsmFloat { Value = r.ReadSingle() },
                ParamDataType.String => Encoding.UTF8.GetString(r.ReadBytes(paramByteDataSize)),
                ParamDataType.FsmEvent when Version == 1 => new FsmEvent { Name = Encoding.UTF8.GetString(r.ReadBytes(paramByteDataSize)) },
                ParamDataType.Vector2 => new Vector2 { X = r.ReadSingle(), Y = r.ReadSingle() },
                ParamDataType.FsmVector2 when Version == 1 => new FsmVector2 { Value = new Vector2 { X = r.ReadSingle(), Y = r.ReadSingle() } },
                ParamDataType.Vector3 => new Vector3 { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle() },
                ParamDataType.FsmVector3 when Version == 1 => new FsmVector3 { Value = new Vector3 { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle() } },
                ParamDataType.Quaternion => new Quaternion { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(), W = r.ReadSingle() },
                ParamDataType.FsmQuaternion when Version == 1 => new FsmQuaternion { Value = new Quaternion { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(), W = r.ReadSingle() } },
                ParamDataType.Color => new EngineColor { R = r.ReadSingle(), G = r.ReadSingle(), B = r.ReadSingle(), A = r.ReadSingle() },
                ParamDataType.FsmColor when Version == 1 => new FsmColor { Value = new EngineColor { R = r.ReadSingle(), G = r.ReadSingle(), B = r.ReadSingle(), A = r.ReadSingle() } },
                ParamDataType.Rect => new Rect { X = r.ReadSingle(), Y = r.ReadSingle(), Width = r.ReadSingle(), Height = r.ReadSingle() },
                ParamDataType.FsmRect when Version == 1 => new FsmRect { Value = new Rect { X = r.ReadSingle(), Y = r.ReadSingle(), Width = r.ReadSingle(), Height = r.ReadSingle() } },
                /////////////////////////////////////////////////////////

                ParamDataType.FsmBool when Version > 1 => actionData.FsmBoolParams[paramDataPos],
                ParamDataType.FsmInt when Version > 1 => actionData.FsmIntParams[paramDataPos],
                ParamDataType.FsmFloat when Version > 1 => actionData.FsmFloatParams[paramDataPos],
                ParamDataType.FsmVector2 when Version > 1 => actionData.FsmVector2Params[paramDataPos],
                ParamDataType.FsmVector3 when Version > 1 => actionData.FsmVector3Params[paramDataPos],
                ParamDataType.FsmQuaternion when Version > 1 => actionData.FsmQuaternionParams[paramDataPos],
                ParamDataType.FsmColor when Version > 1 => actionData.FsmColorParams[paramDataPos],
                ParamDataType.FsmRect when Version > 1 => actionData.FsmRectParams[paramDataPos],
                ///////////////////////////////////////////////////////// 
                ParamDataType.FsmEnum => actionData.FsmEnumParams[paramDataPos],
                ParamDataType.FsmGameObject => actionData.FsmGameObjectParams[paramDataPos],
                ParamDataType.FsmOwnerDefault => actionData.FsmOwnerDefaultParams[paramDataPos],
                ParamDataType.FsmObject => actionData.FsmObjectParams[paramDataPos],
                ParamDataType.FsmVar => actionData.FsmVarParams[paramDataPos],
                ParamDataType.FsmString => actionData.FsmStringParams[paramDataPos],
                ParamDataType.FsmEvent => actionData.StringParams[paramDataPos],
                ParamDataType.FsmEventTarget => actionData.FsmEventTargetParams[paramDataPos],
                ParamDataType.FsmArray => actionData.FsmArrayParams[paramDataPos],
                ParamDataType.ObjectReference => new FsmObject { Value = actionData.UnityObjectParams[paramDataPos] },
                ParamDataType.FunctionCall => actionData.FunctionCallParams[paramDataPos],
                ParamDataType.FsmTemplateControl => actionData.FsmTemplateControlParams[paramDataPos],
                ParamDataType.Array => ConvertActionDataArray(actionData, ref paramIdx),
                ParamDataType.FsmProperty => actionData.FsmPropertyParams[paramDataPos],
                ParamDataType.FsmMaterial => new FsmMaterial(actionData.FsmObjectParams[paramDataPos]),
                ParamDataType.FsmTexture => new FsmTexture(actionData.FsmObjectParams[paramDataPos]),
                _ => $"[{paramDataType} not implemented]",
            };
        }
        catch
        {
            // invalid read (probably out of bounds params array read)
            ret = $"[invalid {paramDataType} variable]";
        }


        if (Version == 1 && ret is NamedVariable namedVar)
        {
            switch (paramDataType)
            {
                case ParamDataType.FsmInt:
                case ParamDataType.FsmBool:
                case ParamDataType.FsmFloat:
                case ParamDataType.FsmVector2:
                case ParamDataType.FsmVector3:
                case ParamDataType.FsmQuaternion:
                case ParamDataType.FsmColor:
                    namedVar.UseVariable = r.ReadBoolean();

                    var nameLength = paramByteDataSize - ((int)r.BaseStream.Position - paramDataPos);
                    namedVar.Name = Encoding.UTF8.GetString(r.ReadBytes(nameLength));
                    break;
            }
        }

        return ret;
    }

    private static bool WillTypeReadBuffer(ParamDataType paramDataType, int version)
    {
        return paramDataType switch
        {
            ParamDataType.Integer => true,
            ParamDataType.FsmInt when version == 1 => true,
            ParamDataType.Enum => true,
            ParamDataType.Boolean => true,
            ParamDataType.FsmBool when version == 1 => true,
            ParamDataType.Float => true,
            ParamDataType.FsmFloat when version == 1 => true,
            ParamDataType.String => true,
            ParamDataType.FsmEvent when version == 1 => true,
            ParamDataType.Vector2 => true,
            ParamDataType.FsmVector2 when version == 1 => true,
            ParamDataType.Vector3 => true,
            ParamDataType.FsmVector3 when version == 1 => true,
            ParamDataType.Quaternion => true,
            ParamDataType.FsmQuaternion when version == 1 => true,
            ParamDataType.Color => true,
            ParamDataType.FsmColor when version == 1 => true,
            ParamDataType.Rect => true,
            ParamDataType.FsmRect when version == 1 => true,
            _ => false,
        };
    }

    private static FsmDocumentNodeFieldValue ConvertObjectToNodeFieldValue(object obj, bool inArray)
    {
        string valueName;
        if (obj is NamedVariable nv)
            valueName = nv.Name;
        else if (obj is FsmOwnerDefault fod)
            valueName = fod.GameObject?.Name ?? string.Empty;
        else
            valueName = string.Empty;

        var indent = inArray ? 1 : 0;

        if (obj is int objInt)
            return new FsmDocumentNodeFieldIntegerValue(objInt, valueName, indent);
        else if (obj is float objFloat)
            return new FsmDocumentNodeFieldFloatValue(objFloat, valueName, indent);
        else if (obj is bool objBool)
            return new FsmDocumentNodeFieldBooleanValue(objBool, valueName, indent);
        else if (obj is string objString)
            return new FsmDocumentNodeFieldStringValue(objString, valueName, indent);
        else if (obj is FsmArrayInfo objArrayInf)
            return new FsmDocumentNodeFieldArrayValue(objArrayInf.TypeName, valueName, objArrayInf.Elements.Length, indent);
        else if (obj is IFsmPlaymakerValuePreviewer objHandler)
            return new FsmPlaymakerValue(objHandler, valueName, indent);
        else
            return new FsmDocumentNodeFieldFallbackValue(obj, indent);

        //return new FsmDocumentNodeFieldStringValue("[unsupported object]");
    }

    private static string TrimFullNameToClassName(string fullName)
    {
        if (fullName.Contains('.'))
            fullName = fullName[(fullName.LastIndexOf('.') + 1)..];

        return fullName;
    }

    private static readonly DrawingColor[] STATE_COLORS =
    [
        DrawingColor.FromArgb(128, 128, 128),
        DrawingColor.FromArgb(116, 143, 201),
        DrawingColor.FromArgb(58, 182, 166),
        DrawingColor.FromArgb(93, 164, 53),
        DrawingColor.FromArgb(225, 254, 50),
        DrawingColor.FromArgb(235, 131, 46),
        DrawingColor.FromArgb(187, 75, 75),
        DrawingColor.FromArgb(117, 53, 164)
    ];

    private static readonly DrawingColor[] TRANSITION_COLORS =
    [
        DrawingColor.FromArgb(222, 222, 222),
        DrawingColor.FromArgb(197, 213, 248),
        DrawingColor.FromArgb(159, 225, 216),
        DrawingColor.FromArgb(183, 225, 159),
        DrawingColor.FromArgb(225, 254, 102),
        DrawingColor.FromArgb(255, 198, 152),
        DrawingColor.FromArgb(225, 159, 160),
        DrawingColor.FromArgb(197, 159, 225)
    ];

    private class FsmArrayInfo(string typeName, object[] elements)
    {
        public string TypeName = typeName;
        public object[] Elements = elements;
    }
}
