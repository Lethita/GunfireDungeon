﻿#if TOOLS

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Config;
using Godot;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Array = Godot.Collections.Array;
using Environment = System.Environment;

namespace Generator;

public static class ExcelGenerator
{
    private static HashSet<string> _excelNames = new HashSet<string>();
    
    private enum CollectionsType
    {
        None,
        Array,
        Map
    }
    
    private class MappingData
    {
        public string TypeStr;
        public string TypeName;
        public CollectionsType CollectionsType;
        
        public bool IsRefExcel;
        public string RefTypeStr;
        public string RefTypeName;

        public MappingData(string typeStr, string typeName, CollectionsType collectionsType)
        {
            TypeStr = typeStr;
            TypeName = typeName;
            CollectionsType = collectionsType;
            IsRefExcel = false;
        }
        
        public MappingData(string typeStr, string typeName, CollectionsType collectionsType, string refTypeStr, string refTypeName)
        {
            TypeStr = typeStr;
            TypeName = typeName;
            CollectionsType = collectionsType;
            IsRefExcel = true;
            RefTypeStr = refTypeStr;
            RefTypeName = refTypeName;
        }
    }

    private class ExcelData
    {
        public string TableName;
        public string OutCode;
        public List<string> ColumnNames = new List<string>();
        public Dictionary<string, MappingData> ColumnMappingData = new Dictionary<string, MappingData>();
        public Dictionary<string, Type> ColumnType = new Dictionary<string, Type>();
        public List<Dictionary<string, object>> DataList = new List<Dictionary<string, object>>();
    }
    
    /// <summary>
    /// 导出 Excel 表
    /// </summary>
    public static void ExportExcel()
    {
        var excelPath = "excel/";
        var jsonPath = "resource/config/";
        var codePath = "src/config/";
        ExportExcel(excelPath, jsonPath, codePath);
    }
    
    /// <summary>
    /// 导出 Excel 表
    /// </summary>
    /// <param name="excelFilePath">excel文件路径</param>
    /// <param name="jsonOutPath">json配置输出路径</param>
    /// <param name="codeOutPath">代码输出路径</param>
    public static bool ExportExcel(string excelFilePath, string jsonOutPath, string codeOutPath)
    {
        _excelNames.Clear();
        Debug.Log("当前路径: " + Environment.CurrentDirectory);
        Debug.Log("excel路径: " + excelFilePath);
        Debug.Log("json输出路径: " + jsonOutPath);
        Debug.Log("cs代码输出路径: " + codeOutPath);
        try
        {
            var excelDataList = new List<ExcelData>();
            
            var directoryInfo = new DirectoryInfo(excelFilePath);
            if (directoryInfo.Exists)
            {
                var fileInfos = directoryInfo.GetFiles();
                //记录文件
                foreach (var fileInfo in fileInfos)
                {
                    var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name).FirstToUpper();
                    _excelNames.Add(fileName);
                }
                //读取配置文件
                foreach (var fileInfo in fileInfos)
                {
                    if (fileInfo.Extension == ".xlsx")
                    {
                        if (fileInfo.Name == "ExcelConfig.xlsx")
                        {
                            throw new Exception("excel表文件名称不允许叫'ExcelConfig.xlsx'!");
                        }
                        Debug.Log("excel表: " + fileInfo.FullName);
                        excelDataList.Add(ReadExcel(fileInfo.FullName));
                    }
                }
            }

            Debug.Log($"一共检测到excel表共{excelDataList.Count}张.");
            if (excelDataList.Count == 0)
            {
                return true;
            }
            
            if (Directory.Exists(codeOutPath))
            {
                Directory.Delete(codeOutPath, true);
            }
            if (Directory.Exists(jsonOutPath))
            {
                Directory.Delete(jsonOutPath, true);
            }
            Directory.CreateDirectory(codeOutPath);
            Directory.CreateDirectory(jsonOutPath);
            
            //保存配置和代码
            foreach (var excelData in excelDataList)
            {
                if (excelData.TableName == "ActivityBase")
                {
                    //生成初始化 ActivityObject 代码
                    GeneratorActivityObjectInit(excelData);
                }
                File.WriteAllText(codeOutPath + "ExcelConfig_" + excelData.TableName + ".cs", excelData.OutCode);
                var config = new JsonSerializerOptions();
                config.WriteIndented = true;
                File.WriteAllText(jsonOutPath + excelData.TableName + ".json", JsonSerializer.Serialize(excelData.DataList, config));
            }
            
            //生成加载代码
            var code = GeneratorInitCode(excelDataList);
            File.WriteAllText(codeOutPath + "ExcelConfig.cs", code);
        }
        catch (Exception e)
        {
            PrintError(e.ToString());
            return false;
        }

        return true;
    }

    private static string GeneratorInitCode(List<ExcelData> excelList)
    {
        var code = $"using System;\n";
        code += $"using System.Collections.Generic;\n";
        code += $"using System.Text.Json;\n";
        code += $"using Godot;\n";
        code += $"\n";
        code += $"namespace Config;\n";
        code += $"\n";
        code += $"public static partial class ExcelConfig\n";
        code += $"{{\n";

        var fieldCode = "";
        var callFuncCode = "";
        var callInitRefFuncCode = "";
        var funcCode = "";
        var initRefFuncCode = "";
        
        foreach (var excelData in excelList)
        {
            var idName = excelData.ColumnNames[0];
            var idTypeStr = excelData.ColumnMappingData[idName].TypeStr;
            
            //---------------------------- 引用其他表处理 ----------------------------
            var hasRefColumn = false;
            var refColumnNoneCode = "";
            foreach (var columnName in excelData.ColumnNames)
            {
                var mappingData = excelData.ColumnMappingData[columnName];
                if (mappingData.IsRefExcel)
                {
                    hasRefColumn = true;
                    if (mappingData.CollectionsType == CollectionsType.None)
                    {
                        refColumnNoneCode += $"                if (!string.IsNullOrEmpty(item.__{columnName}))\n";
                        refColumnNoneCode += $"                {{\n";
                        refColumnNoneCode += $"                    item.{columnName} = {mappingData.RefTypeName}_Map[item.__{columnName}];\n";
                        refColumnNoneCode += $"                }}\n";
                    }
                    else if (mappingData.CollectionsType == CollectionsType.Array)
                    {
                        refColumnNoneCode += $"                if (item.__{columnName} != null)\n";
                        refColumnNoneCode += $"                {{\n";
                        refColumnNoneCode += $"                    item.{columnName} = new {mappingData.RefTypeName}[item.__{columnName}.Length];\n";
                        refColumnNoneCode += $"                    for (var i = 0; i < item.__{columnName}.Length; i++)\n";
                        refColumnNoneCode += $"                    {{\n";
                        refColumnNoneCode += $"                        item.{columnName}[i] = {mappingData.RefTypeName}_Map[item.__{columnName}[i]];\n";
                        refColumnNoneCode += $"                    }}\n";
                        refColumnNoneCode += $"                }}\n";
                    }
                    else
                    {
                        refColumnNoneCode += $"                if (item.__{columnName} != null)\n";
                        refColumnNoneCode += $"                {{\n";
                        refColumnNoneCode += $"                    item.{columnName} = new {mappingData.RefTypeStr}();\n";
                        refColumnNoneCode += $"                    foreach (var pair in item.__{columnName})\n";
                        refColumnNoneCode += $"                    {{\n";
                        refColumnNoneCode += $"                        item.{columnName}.Add(pair.Key, {mappingData.RefTypeName}_Map[pair.Value]);\n";
                        refColumnNoneCode += $"                    }}\n";
                        refColumnNoneCode += $"                }}\n";
                    }
                    refColumnNoneCode += $"\n";
                }
            }
            
            //----------------------------- 数据集合 ------------------------------------
            fieldCode += $"    /// <summary>\n";
            fieldCode += $"    /// {excelData.TableName}.xlsx表数据集合, 以 List 形式存储, 数据顺序与 Excel 表相同\n";
            fieldCode += $"    /// </summary>\n";
            fieldCode += $"    public static List<{excelData.TableName}> {excelData.TableName}_List {{ get; private set; }}\n";
            fieldCode += $"    /// <summary>\n";
            fieldCode += $"    /// {excelData.TableName}.xlsx表数据集合, 里 Map 形式存储, key 为 {idName}\n";
            fieldCode += $"    /// </summary>\n";
            fieldCode += $"    public static Dictionary<{idTypeStr}, {excelData.TableName}> {excelData.TableName}_Map {{ get; private set; }}\n";
            fieldCode += $"\n";
            
            //------------------------------- 初始化函数 -------------------------------------
            callFuncCode += $"        _Init{excelData.TableName}Config();\n";
            
            funcCode += $"    private static void _Init{excelData.TableName}Config()\n";
            funcCode += $"    {{\n";
            funcCode += $"        try\n";
            funcCode += $"        {{\n";
            funcCode += $"            var text = _ReadConfigAsText(\"res://resource/config/{excelData.TableName}.json\");\n";
            if (hasRefColumn) //存在引用列
            {
                funcCode += $"            {excelData.TableName}_List = new List<{excelData.TableName}>(JsonSerializer.Deserialize<List<Ref_{excelData.TableName}>>(text));\n";
            }
            else
            {
                funcCode += $"            {excelData.TableName}_List = JsonSerializer.Deserialize<List<{excelData.TableName}>>(text);\n";
            }
            funcCode += $"            {excelData.TableName}_Map = new Dictionary<{idTypeStr}, {excelData.TableName}>();\n";
            funcCode += $"            foreach (var item in {excelData.TableName}_List)\n";
            funcCode += $"            {{\n";
            funcCode += $"                {excelData.TableName}_Map.Add(item.{idName}, item);\n";
            funcCode += $"            }}\n";
            funcCode += $"        }}\n";
            funcCode += $"        catch (Exception e)\n";
            funcCode += $"        {{\n";
            funcCode += $"            GD.PrintErr(e.ToString());\n";
            funcCode += $"            throw new Exception(\"初始化表'{excelData.TableName}'失败!\");\n";
            funcCode += $"        }}\n";
            funcCode += $"    }}\n";

            
            //------------------------------- 初始化引用 ---------------------------------
            if (hasRefColumn)
            {
                callInitRefFuncCode += $"        _Init{excelData.TableName}Ref();\n";

                initRefFuncCode += $"    private static void _Init{excelData.TableName}Ref()\n";
                initRefFuncCode += $"    {{\n";
                initRefFuncCode += $"        foreach (Ref_{excelData.TableName} item in {excelData.TableName}_List)\n";
                initRefFuncCode += $"        {{\n";
                initRefFuncCode += $"            try\n";
                initRefFuncCode += $"            {{\n";
                initRefFuncCode += refColumnNoneCode;
                initRefFuncCode += $"            }}\n";
                initRefFuncCode += $"            catch (Exception e)\n";
                initRefFuncCode += $"            {{\n";
                initRefFuncCode += $"                GD.PrintErr(e.ToString());\n";
                initRefFuncCode += $"                throw new Exception(\"初始化'{excelData.TableName}'引用其他表数据失败, 当前行id: \" + item.Id);\n";
                initRefFuncCode += $"            }}\n";
                initRefFuncCode += $"        }}\n";
                initRefFuncCode += $"    }}\n";
            }
        }

        code += fieldCode;
        code += $"\n";
        code += $"    private static bool _init = false;\n";
        code += $"    /// <summary>\n";
        code += $"    /// 初始化所有配置表数据\n";
        code += $"    /// </summary>\n";
        code += $"    public static void Init()\n";
        code += $"    {{\n";
        code += $"        if (_init) return;\n";
        code += $"        _init = true;\n";
        code += $"\n";
        code += callFuncCode;
        code += $"\n";
        code += callInitRefFuncCode;
        code += $"    }}\n";
        code += funcCode;
        code += $"\n";
        code += initRefFuncCode;
        code += $"    private static string _ReadConfigAsText(string path)\n";
        code += $"    {{\n";
        code += $"        var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);\n";
        code += $"        var asText = file.GetAsText();\n";
        code += $"        file.Dispose();\n";
        code += $"        return asText;\n";
        code += $"    }}\n";
        code += $"}}";
        return code;
    }
    
    private static ExcelData ReadExcel(string excelPath)
    {
        var excelData = new ExcelData();
        //文件名称
        var fileName = Path.GetFileNameWithoutExtension(excelPath).FirstToUpper();
        excelData.TableName = fileName;
        //输出代码
        var outStr = $"using System.Text.Json.Serialization;\n";
        outStr += $"using System.Collections.Generic;\n\n";
        outStr += $"namespace Config;\n\n";
        outStr += $"public static partial class ExcelConfig\n{{\n";
        outStr += $"    public class {fileName}\n";
        outStr += $"    {{\n";
        //继承的带有引用其他表的类代码
        var outRefStr = "";
        
        var cloneFuncStr = $"        /// <summary>\n";
        cloneFuncStr += $"        /// 返回浅拷贝出的新对象\n";
        cloneFuncStr += $"        /// </summary>\n";
        cloneFuncStr += $"        public {fileName} Clone()\n";
        cloneFuncStr += $"        {{\n";
        cloneFuncStr += $"            var inst = new {fileName}();\n";
        
        var sourceFile = excelPath;

        //行数
        var rowCount = -1;
        //列数
        var columnCount = -1;
        
        //加载表数据
        var workbook = new XSSFWorkbook(sourceFile);
        using (workbook)
        {
            var sheet1 = workbook.GetSheet("Sheet1");
            rowCount = sheet1.LastRowNum;
            //先解析表中的列名, 注释, 类型
            var names = sheet1.GetRow(0);
            var descriptions = sheet1.GetRow(1);
            var types = sheet1.GetRow(2);
            columnCount = names.LastCellNum;
            foreach (var cell in names)
            {
                //字段名称
                var field = GetCellStringValue(cell);
                if (string.IsNullOrEmpty(field))
                {
                    if (cell.ColumnIndex == 0)
                    {
                        throw new Exception($"表'{fileName}'的列数为0!");
                    }
                    //到达最后一列了
                    columnCount = cell.ColumnIndex;
                    break;
                }
                field = field.FirstToUpper();
                excelData.ColumnNames.Add(field);
                if (field == "Clone")
                {
                    throw new Exception($"表'{fileName}'中不允许有'Clone'字段!");
                }

                var descriptionCell = descriptions.GetCell(cell.ColumnIndex);
                //描述
                string description;
                if (descriptionCell != null)
                {
                    description = GetCellStringValue(descriptionCell).Replace("\n", " <br/>\n        /// ");
                }
                else
                {
                    description = "";
                }
                //类型
                var typeString = GetCellStringValue(types.GetCell(cell.ColumnIndex));
                if (string.IsNullOrEmpty(typeString))
                {
                    throw new Exception($"表'{fileName}'中'{field}'这一列类型为空!");
                }
                
                //尝试解析类型
                MappingData mappingData;
                try
                {
                    mappingData = ConvertToType(typeString.Replace(" ", ""));
                }
                catch (Exception e)
                {
                    PrintError(e.ToString());
                    throw new Exception($"表'{fileName}'中'{field}'这一列类型描述语法错误: {typeString}");
                }
                
                if (!excelData.ColumnMappingData.TryAdd(field, mappingData))
                {
                    throw new Exception($"表'{fileName}'中存在相同名称的列: '{field}'!");
                }
                outStr += $"        /// <summary>\n";
                outStr += $"        /// {description}\n";
                outStr += $"        /// </summary>\n";
                if (!mappingData.IsRefExcel) //没有引用其他表
                {
                    outStr += $"        [JsonInclude]\n";
                    outStr += $"        public {mappingData.TypeStr} {field};\n\n";
                }
                else
                {
                    outStr += $"        public {mappingData.RefTypeStr} {field};\n\n";
                }

                if (mappingData.IsRefExcel) //引用其他表
                {
                    if (string.IsNullOrEmpty(outRefStr))
                    {
                        outRefStr += $"    private class Ref_{fileName} : {fileName}\n";
                        outRefStr += $"    {{\n";
                    }
                    outRefStr += $"        [JsonInclude]\n";
                    outRefStr += $"        public {mappingData.TypeStr} __{field};\n\n";
                }
                
                cloneFuncStr += $"            inst.{field} = {field};\n";
            }
        
            cloneFuncStr += "            return inst;\n";
            cloneFuncStr += "        }\n";
            outStr += cloneFuncStr;
            outStr += "    }\n";

            if (!string.IsNullOrEmpty(outRefStr))
            {
                outRefStr += "    }\n";
                outStr += outRefStr;
            }
            
            outStr += "}";
            
            //解析字段类型
            foreach (var kv in excelData.ColumnMappingData)
            {
                var typeName = kv.Value.TypeName;
                var type = Type.GetType(typeName);
                if (type == null)
                {
                    throw new Exception($"表'{fileName}'中'{kv.Key}'这一列类型未知! " + kv.Value.TypeStr);
                }
                excelData.ColumnType.Add(kv.Key, type);
            }

            //解析数据
            for (int i = 3; i <= rowCount; i++)
            {
                Dictionary<string, object> data = null;
                var row = sheet1.GetRow(i);
                if (row == null)
                {
                    continue;
                }
                for (int j = 0; j < columnCount; j++)
                {
                    var cell = row.GetCell(j);
                    var strValue = GetCellStringValue(cell);
                    //如果这一行的第一列数据为空, 则跳过这一行
                    if (j == 0 && string.IsNullOrEmpty(strValue))
                    {
                        break;
                    }
                    else if (data == null)
                    {
                        data = new Dictionary<string, object>();
                        excelData.DataList.Add(data);
                    }

                    var fieldName = excelData.ColumnNames[j];
                    var mappingData = excelData.ColumnMappingData[fieldName];
                    var field = mappingData.IsRefExcel ? "__" + fieldName : fieldName;
                    try
                    {
                        switch (mappingData.TypeStr)
                        {
                            case "bool":
                            case "boolean":
                                data.Add(field, GetCellBooleanValue(cell));
                                break;
                            case "byte":
                                data.Add(field, Convert.ToByte(GetCellNumberValue(cell)));
                                break;
                            case "sbyte":
                                data.Add(field, Convert.ToSByte(GetCellNumberValue(cell)));
                                break;
                            case "short":
                                data.Add(field, Convert.ToInt16(GetCellNumberValue(cell)));
                                break;
                            case "ushort":
                                data.Add(field, Convert.ToUInt16(GetCellNumberValue(cell)));
                                break;
                            case "int":
                                data.Add(field, Convert.ToInt32(GetCellNumberValue(cell)));
                                break;
                            case "uint":
                                data.Add(field, Convert.ToUInt32(GetCellNumberValue(cell)));
                                break;
                            case "long":
                                data.Add(field, Convert.ToInt64(GetCellNumberValue(cell)));
                                break;
                            case "ulong":
                                data.Add(field, Convert.ToUInt64(GetCellNumberValue(cell)));
                                break;
                            case "float":
                                data.Add(field, Convert.ToSingle(GetCellNumberValue(cell)));
                                break;
                            case "double":
                                data.Add(field, GetCellNumberValue(cell));
                                break;
                            case "string":
                                data.Add(field, GetCellStringValue(cell));
                                break;
                            default:
                            {
                                var cellStringValue = GetCellStringValue(cell);
                                if (cellStringValue.Length == 0)
                                {
                                    data.Add(field, null);
                                }
                                else
                                {
                                    data.Add(field, JsonSerializer.Deserialize(cellStringValue, excelData.ColumnType[fieldName]));
                                }
                            }
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        PrintError(e.ToString());
                        throw new Exception($"解析表'{fileName}'第'{i + 1}'行第'{j + 1}'列数据时发生异常");
                    }
                }
            }
        }

        excelData.OutCode = outStr;
        return excelData;
    }

    private static string GetCellStringValue(ICell cell)
    {
        if (cell == null)
        {
            return "";
        }
        switch (cell.CellType)
        {
            case CellType.Numeric:
                return cell.NumericCellValue.ToString();
            case CellType.String:
                return cell.StringCellValue;
            case CellType.Formula:
                return cell.CellFormula;
            case CellType.Boolean:
                return cell.BooleanCellValue ? "true" : "false";
        }

        return "";
    }

    private static double GetCellNumberValue(ICell cell)
    {
        if (cell == null)
        {
            return 0;
        }

        return cell.NumericCellValue;
    }

    private static bool GetCellBooleanValue(ICell cell)
    {
        if (cell == null)
        {
            return false;
        }

        if (cell.CellType == CellType.Boolean)
        {
            return cell.BooleanCellValue;
        }

        var value = cell.StringCellValue;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return bool.Parse(value);
    }

    private static MappingData ConvertToType(string str, int depth = 0)
    {
        if (Regex.IsMatch(str, "^\\w+$"))
        {
            var typeStr = TypeStrMapping(str);
            var typeName = TypeNameMapping(str);
            return new MappingData(typeStr, typeName, CollectionsType.None);
        }
        else if (Regex.IsMatch(str, "^\\$\\w+$")) //引用其他表
        {
            var realName = str.Substring(1);
            if (!_excelNames.Contains(realName))
            {
                throw new Exception($"引用表数据失败, 未找到表: {realName}!");
            }

            if (depth > 1)
            {
                throw new Exception("引用表数据失败, 引用表数据仅支持放入第一层的数组和字典!");
            }

            return new MappingData(TypeStrMapping("string"), TypeNameMapping("string"), CollectionsType.None, realName, realName);
        }
        else if (str.StartsWith('{')) //字典
        {
            var tempStr = str.Substring(1, str.Length - 2);
            var index = tempStr.IndexOf(':');
            if (index == -1)
            {
                throw new Exception("类型描述语法错误!");
            }

            var keyStr = tempStr.Substring(0, index);
            if (!IsBaseType(keyStr))
            {
                throw new Exception($"字典key类型必须是基础类型!");
            }

            var type1 = ConvertToType(keyStr, depth + 1);
            var type2 = ConvertToType(tempStr.Substring(index + 1), depth + 1);

            var typeStr = $"Dictionary<{type1.TypeStr}, {type2.TypeStr}>";
            var typeName = $"System.Collections.Generic.Dictionary`2[[{type1.TypeName}],[{type2.TypeName}]]";

            if (type2.IsRefExcel) //引用过其他表
            {
                var refTypeStr = $"Dictionary<{type1.TypeStr}, {type2.RefTypeStr}>";
                return new MappingData(typeStr, typeName, CollectionsType.Map, refTypeStr, type2.RefTypeName);
            }

            return new MappingData(typeStr, typeName, CollectionsType.Map);
        }
        else if (str.StartsWith('[')) //数组
        {
            var tempStr = str.Substring(1, str.Length - 2);
            var typeData = ConvertToType(tempStr, depth + 1);
            var typeStr = typeData.TypeStr + "[]";
            var typeName = typeData.TypeName + "[]";

            if (typeData.IsRefExcel) //引用过其他表
            {
                var refTypeStr = typeData.RefTypeStr + "[]";
                return new MappingData(typeStr, typeName, CollectionsType.Array, refTypeStr, typeData.RefTypeName);
            }
            
            return new MappingData(typeStr, typeName, CollectionsType.Array);
        }
        throw new Exception("类型描述语法错误!");
    }
    
    private static string TypeStrMapping(string typeName)
    {
        switch (typeName)
        {
            case "boolean": return "bool";
            case "vector2": return "SerializeVector2";
            case "vector3": return "SerializeVector3";
            case "color": return "SerializeColor";
        }

        return typeName;
    }

    private static string TypeNameMapping(string typeName)
    {
        switch (typeName)
        {
            case "bool":
            case "boolean": return typeof(bool).FullName;
            case "byte": return typeof(byte).FullName;
            case "sbyte": return typeof(sbyte).FullName;
            case "short": return typeof(short).FullName;
            case "ushort": return typeof(ushort).FullName;
            case "int": return typeof(int).FullName;
            case "uint": return typeof(uint).FullName;
            case "long": return typeof(long).FullName;
            case "ulong": return typeof(ulong).FullName;
            case "string": return typeof(string).FullName;
            case "float": return typeof(float).FullName;
            case "double": return typeof(double).FullName;
            case "vector2": return "SerializeVector2";
            case "vector3": return "SerializeVector3";
            case "color": return "SerializeColor";
        }

        return typeName;
    }

    private static bool IsBaseType(string typeName)
    {
        switch (typeName)
        {
            case "bool":
            case "boolean":
            case "byte":
            case "sbyte":
            case "short":
            case "ushort":
            case "int":
            case "uint":
            case "long":
            case "ulong":
            case "string":
            case "float":
            case "double":
                return true;
        }

        return false;
    }
    
    private static void PrintError(string message)
    {
        Debug.LogError(message);
    }
    
    //生成 ActivityObject.Ids 代码
    private static void GeneratorActivityObjectInit(ExcelData activityExcelData)
    {
        var code1 = "";
        foreach (var item in activityExcelData.DataList)
        {
            var id = item["Id"];
            var name = item["Name"] + "";
            var intro = item["Intro"] + "";
            code1 += $"        /// <summary>\n";
            code1 += $"        /// 名称: {name} <br/>\n";
            code1 += $"        /// 简介: {intro.Replace("\n", " <br/>\n        /// ")}\n";
            code1 += $"        /// </summary>\n";
            code1 += $"        public const string Id_{id} = \"{id}\";\n";
        }
        
        var str = $"using Config;\n\n";
        str += $"// 根据配置表注册物体, 该类是自动生成的, 请不要手动编辑!\n";
        str += $"public partial class ActivityObject\n";
        str += $"{{\n";
        
        str += $"    /// <summary>\n";
        str += $"    /// 存放所有在表中注册的物体的id\n";
        str += $"    /// </summary>\n";
        str += $"    public static class Ids\n";
        str += $"    {{\n";
        str += code1;
        str += $"    }}\n";
        
        str += $"}}\n";
        
        File.WriteAllText("src/framework/activity/ActivityObject_Init.cs", str);
    }
}

#endif