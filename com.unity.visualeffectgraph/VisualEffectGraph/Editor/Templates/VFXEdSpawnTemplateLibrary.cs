using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Reflection;
using UnityEngine;
using UnityEditor.Experimental;
using UnityEditor.Experimental.Graph;
using Object = UnityEngine.Object;

namespace UnityEditor.Experimental
{
    public class VFXEdSpawnTemplateLibrary : ScriptableObject
    {
        public static string LibraryPath = "/VFXEditor/Editor/TemplateLibrary.txt";

        public List<VFXEdSpawnTemplate> Templates { get { return m_Templates; } }
        [SerializeField]
        private List<VFXEdSpawnTemplate> m_Templates;

        public VFXEdSpawnTemplateLibrary()
        {
            m_Templates = new List<VFXEdSpawnTemplate>();
        }

        public VFXEdSpawnTemplate GetTemplate(string path)
        {
            return m_Templates.Find(t => t.Path.Equals(path));
        }

        public void SpawnFromMenu(object o)
        {
            VFXEdTemplateSpawner spawner = o as VFXEdTemplateSpawner;
            spawner.Spawn();
        }

        public void DeleteTemplate (string Path)
        {
            VFXEdSpawnTemplate t = GetTemplate(Path);
            if (t != null)
            {
                m_Templates.Remove(t);
                WriteLibrary();
            }
        }

        public void AddTemplate(VFXEdSpawnTemplate template)
        {
            VFXEdSpawnTemplate todelete = m_Templates.Find(t => t.Path.Equals(template.Path));

            if (todelete != null)
                if (EditorUtility.DisplayDialog("Template Already Exists", "Template Already Exists, Overwrite?", "Overwrite", "Cancel"))
                {
                    m_Templates.Remove(todelete);
                }
                else
                    return;

            m_Templates.Add(template);
            WriteLibrary();

        }

        public static VFXEdSpawnTemplateLibrary Create()
        {
            VFXEdSpawnTemplateLibrary lib = CreateInstance<VFXEdSpawnTemplateLibrary>();
            lib.ReloadLibrary();
            return lib;
        }

        public VFXParamValue CreateParamValue(string ParamType, string XMLStringValue)
        {
            string[] vals;

            switch(ParamType)
            {
                case "kTypeFloat":
                    return VFXParamValue.Create(float.Parse(XMLStringValue));
                case "kTypeInt":
                    return VFXParamValue.Create(int.Parse(XMLStringValue));
                case "kTypeUint":
                    return VFXParamValue.Create(uint.Parse(XMLStringValue));
                case "kTypeFloat2":
                    vals = XMLStringValue.Split(',');
                    Vector2 v2 = new Vector2(float.Parse(vals[0]), float.Parse(vals[1]));
                    return VFXParamValue.Create(v2);
                case "kTypeFloat3":
                    vals = XMLStringValue.Split(',');
                    Vector3 v3 = new Vector3(float.Parse(vals[0]), float.Parse(vals[1]), float.Parse(vals[2]));
                    return VFXParamValue.Create(v3);
                case "kTypeFloat4":
                    vals = XMLStringValue.Split(',');
                    Vector4 v4 = new Vector4(float.Parse(vals[0]), float.Parse(vals[1]), float.Parse(vals[2]), float.Parse(vals[3]));
                    return VFXParamValue.Create(v4);
                case "kTypeTexture2D":
                    return VFXParamValue.Create(AssetDatabase.LoadAssetAtPath<Texture2D>(XMLStringValue));
                case "kTypeTexture3D":
                    return VFXParamValue.Create(AssetDatabase.LoadAssetAtPath<Texture3D>(XMLStringValue));
                default:
                    return null;
            }
        }

        public void ReloadLibrary()
        {
            Templates.Clear();
            string path = Application.dataPath + LibraryPath;
            XDocument doc;

            try
            {
                doc = XDocument.Load(path);
            }
            catch (System.IO.FileNotFoundException e)
            {
                WriteLibrary();
                doc = XDocument.Load(path);
            }
           
            XElement lib = doc.Element("Library");
            var templates = lib.Elements("Template");
            foreach(XElement t in templates)
            {
                VFXEdSpawnTemplate template = VFXEdSpawnTemplate.Create(t.Attribute("Category").Value, t.Attribute("Name").Value);
                var nodes = t.Element("Nodes").Elements("Node");
                var flowconnections = t.Element("Connections").Elements("FlowConnection");

                foreach(XElement n in nodes)
                {
                    template.AddContextNode(n.Attribute("Name").Value, n.Attribute("Context").Value);

                    XElement contextParms = n.Element("Context");

                    foreach(XElement parm in contextParms.Elements("VFXParamValue"))
                    {
                        string nn = n.Attribute("Name").Value;
                        string pn = parm.Attribute("Name").Value;
                        string pt = parm.Attribute("Type").Value;

                        VFXParamValue value = CreateParamValue(pt, parm.Attribute("Value").Value);
                        template.SetContextNodeParameter(nn, pn, value);
                    }

                    foreach(XElement nb in n.Elements("NodeBlock"))
                    {
                        template.AddContextNodeBlock(n.Attribute("Name").Value, nb.Attribute("Name").Value);

                        foreach(XElement parm in nb.Elements("VFXParamValue"))
                        {
                            string nn = n.Attribute("Name").Value;
                            string nbn = nb.Attribute("Name").Value;
                            string pn = parm.Attribute("Name").Value;
                            string pt = parm.Attribute("Type").Value;

                            VFXParamValue value = CreateParamValue(pt, parm.Attribute("Value").Value);
                            template.SetContextNodeBlockParameter(nn, nbn, pn, value);
                        }
                    }

                }

                foreach(XElement fc in flowconnections)
                {
                    template.AddConnection(fc.Attribute("Previous").Value, fc.Attribute("Next").Value);
                }

                AddTemplate(template);
            }

        }

        public void WriteLibrary()
        {
            string path = Application.dataPath + LibraryPath;

            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace
            };

            XmlWriter doc = XmlWriter.Create(path, settings);

            doc.WriteStartElement("Library");
            foreach(VFXEdSpawnTemplate template in Templates)
            {
                doc.WriteStartElement("Template");
                doc.WriteAttributeString("Category", template.Category);
                doc.WriteAttributeString("Name", template.Name);

                doc.WriteStartElement("Nodes");
                foreach(KeyValuePair<string, ContextNodeInfo> kvp_node in template.ContextNodes)
                {
                    doc.WriteStartElement("Node");
                    doc.WriteAttributeString("Name", kvp_node.Key);
                    doc.WriteAttributeString("Context", kvp_node.Value.Context.ToString());

                    // Context Parameters
                    doc.WriteStartElement("Context");
                    foreach(KeyValuePair<string, VFXParamValue> kvp_param in kvp_node.Value.ParameterOverrides)
                    {
                        doc.WriteStartElement("VFXParamValue");
                        doc.WriteAttributeString("Name", kvp_param.Key);
                        VFXParam.Type type = kvp_param.Value.ValueType;
                        doc.WriteAttributeString("Type", type.ToString());

                        string value = "";
                        switch(type)
                        {
                            case VFXParam.Type.kTypeFloat: value = kvp_param.Value.GetValue<float>().ToString(); break;
                            case VFXParam.Type.kTypeInt: value = kvp_param.Value.GetValue<int>().ToString(); break;
                            case VFXParam.Type.kTypeUint: value = kvp_param.Value.GetValue<uint>().ToString(); break;
                            case VFXParam.Type.kTypeFloat2:
                                Vector2 v2 = kvp_param.Value.GetValue<Vector2>();
                                value = v2.x + "," + v2.y ;
                                break;
                            case VFXParam.Type.kTypeFloat3:
                                Vector3 v3 = kvp_param.Value.GetValue<Vector3>();
                                value = v3.x + "," + v3.y+ "," + v3.z ;
                                break;
                            case VFXParam.Type.kTypeFloat4:
                                Vector4 v4 = kvp_param.Value.GetValue<Vector4>();
                                value = v4.x + "," + v4.y + "," + v4.z+ "," + v4.w;
                                break;
                            case VFXParam.Type.kTypeTexture2D:
                                Texture2D t = kvp_param.Value.GetValue<Texture2D>();
                                value = AssetDatabase.GetAssetPath(t) ;
                                break;
                            case VFXParam.Type.kTypeTexture3D:
                                Texture3D t3 = kvp_param.Value.GetValue<Texture3D>();
                                value = AssetDatabase.GetAssetPath(t3) ;
                                break;
                            default:
                                break;
                        }
                        doc.WriteAttributeString("Value", value);
                        doc.WriteEndElement();
                            
                    }
                    doc.WriteEndElement(); // End Context

                    foreach(KeyValuePair<string, NodeBlockInfo> kvp_nodeblock in kvp_node.Value.nodeBlocks)
                    {
                        doc.WriteStartElement("NodeBlock");
                        doc.WriteAttributeString("Name", kvp_nodeblock.Key);
                        doc.WriteAttributeString("BlockName", kvp_nodeblock.Value.BlockName);

                        foreach(KeyValuePair<string, VFXParamValue> kvp_param in kvp_nodeblock.Value.ParameterOverrides)
                        {
                            doc.WriteStartElement("VFXParamValue");
                            doc.WriteAttributeString("Name", kvp_param.Key);
                            VFXParam.Type type = kvp_param.Value.ValueType;
                            doc.WriteAttributeString("Type", type.ToString());

                            string value = "";
                            switch(type)
                            {
                                case VFXParam.Type.kTypeFloat: value = kvp_param.Value.GetValue<float>().ToString(); break;
                                case VFXParam.Type.kTypeInt: value = kvp_param.Value.GetValue<int>().ToString(); break;
                                case VFXParam.Type.kTypeUint: value = kvp_param.Value.GetValue<uint>().ToString(); break;
                                case VFXParam.Type.kTypeFloat2:
                                    Vector2 v2 = kvp_param.Value.GetValue<Vector2>();
                                    value = v2.x + "," + v2.y ;
                                    break;
                                case VFXParam.Type.kTypeFloat3:
                                    Vector3 v3 = kvp_param.Value.GetValue<Vector3>();
                                    value = v3.x + "," + v3.y+ "," + v3.z ;
                                    break;
                                case VFXParam.Type.kTypeFloat4:
                                    Vector4 v4 = kvp_param.Value.GetValue<Vector4>();
                                    value = v4.x + "," + v4.y + "," + v4.z+ "," + v4.w;
                                    break;
                                case VFXParam.Type.kTypeTexture2D:
                                    Texture2D t = kvp_param.Value.GetValue<Texture2D>();
                                    value = AssetDatabase.GetAssetPath(t) ;
                                    break;
                                case VFXParam.Type.kTypeTexture3D:
                                    Texture3D t3 = kvp_param.Value.GetValue<Texture3D>();
                                    value = AssetDatabase.GetAssetPath(t3) ;
                                    break;
                                default:
                                    break;
                            }
                            doc.WriteAttributeString("Value", value);
                            doc.WriteEndElement();
                            
                        }
                        
                        doc.WriteEndElement(); // End NodeBlock
                    }

                    doc.WriteEndElement(); // End Node
                }
                doc.WriteEndElement(); // End Nodes

                doc.WriteStartElement("Connections");
                foreach(FlowConnection c in template.Connections)
                {
                    doc.WriteStartElement("FlowConnection");
                    foreach(KeyValuePair<string,ContextNodeInfo> kvp_node in template.ContextNodes )
                    {
                        if(kvp_node.Value == c.Previous) doc.WriteAttributeString("Previous", kvp_node.Key);
                        if(kvp_node.Value == c.Next) doc.WriteAttributeString("Next", kvp_node.Key);
                    }
                    
                    doc.WriteEndElement();
                }
                doc.WriteEndElement(); // End Connections

                doc.WriteEndElement(); // End Template
            }

            doc.WriteEndElement(); // End Library
            doc.Close();
        }

        public void CreateDefaultAsset()
        {
            WriteLibrary();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static VFXEdSpawnTemplate CreateTemplateFromSelection(VFXEdCanvas canvas, string category, string name)
        {
            VFXEdSpawnTemplate t = VFXEdSpawnTemplate.Create(category, name);
            if(canvas.selection.Count == 0)
            {
                if (EditorUtility.DisplayDialog("Warning", "Selection is Empty, Are you sure you want to continue?", "Break", "Continue"))
                {
                    return null;
                }
            }
            foreach(CanvasElement e in canvas.selection)
            {
                if(e is VFXEdContextNode)
                {
                    VFXEdContextNode node = (e as VFXEdContextNode);
                    t.AddContextNode(node.UniqueName, node.Model.Desc.Name);

                    // Context Node Parameters
                    for(int i = 0; i < node.Model.GetNbParamValues(); i++)
                    {
                        t.SetContextNodeParameter(node.UniqueName, node.Model.Desc.m_Params[i].m_Name, node.Model.GetParamValue(i).Clone());
                    }

                    // Context Node Blocks
                    foreach(VFXEdProcessingNodeBlock block in node.NodeBlockContainer.nodeBlocks)
                    {
                        t.AddContextNodeBlock(node.UniqueName, block.name);
                        for (int i = 0 ;  i < block.Params.Length; i++)
                        {
                            t.SetContextNodeBlockParameter(node.UniqueName, block.name, block.Params[i].m_Name, block.ParamValues[i].Clone());
                        }
                    }
                }
            }

            foreach(CanvasElement e in canvas.selection)
            {
                if(e is FlowEdge)
                {
                    FlowEdge edge = (e as FlowEdge);
                    if((edge.Left as VFXEdFlowAnchor).parent is VFXEdContextNode && (edge.Right as VFXEdFlowAnchor).parent is VFXEdContextNode)
                    {
                        string left = ((edge.Left as VFXEdFlowAnchor).parent as VFXEdNode).UniqueName;
                        string right = ((edge.Right as VFXEdFlowAnchor).parent as VFXEdNode).UniqueName;

                        if(t.ContextNodes.ContainsKey(left) && t.ContextNodes.ContainsKey(right))
                        {
                            t.AddConnection(left, right);
                        }
                        
                    }

                }
            }
            return t;
        }
        
    }
}
