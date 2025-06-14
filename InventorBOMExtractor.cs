using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using OfficeOpenXml; // Certifique-se de que o pacote NuGet 'EPPlus' está instalado

namespace InventorBOMExtractor
{
    
        

        
    // Classe para representar uma montagem disponível
    public class AssemblyInfo
    {
        public string DisplayName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public dynamic Document { get; set; } = null!;
    }

    // Classe COM Helper (mesma de antes)
    internal static class ComHelper
    {
        [DllImport("ole32.dll")]
        private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string progId, out Guid clsid);

        [DllImport("oleaut32.dll")]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        public static object? GetActiveObject(string progId)
        {
            try
            {
                var result = CLSIDFromProgID(progId, out Guid clsid);
                if (result != 0) return null;

                result = GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
                if (result != 0) return null;

                return obj;
            }
            catch
            {
                return null;
            }
        }
    }

    public class InventorBomExtractor
    {
        private dynamic? _inventorApp;

        public void ConnectToInventor()
        {
            // Tenta conectar à instância ativa primeiro
            try
            {
                Console.WriteLine("Tentando conectar à instância ativa...");
                var activeApp = ComHelper.GetActiveObject("Inventor.Application");

                if (activeApp != null)
                {
                    _inventorApp = activeApp;
                    string versao = _inventorApp.SoftwareVersion.DisplayVersion;
                    Console.WriteLine($"✓ Conectado à instância ativa - Inventor {versao}");

                    // Adicionado: Verifique e torne a instância visível se ela não estiver
                    // Isso pode ajudar a "reanimar" instâncias em segundo plano para que reportem seus documentos corretamente.
                    if (!_inventorApp.Visible)
                    {
                        Console.WriteLine("  Instância ativa do Inventor não visível. Tentando torná-la visível...");
                        _inventorApp.Visible = true;
                        System.Threading.Thread.Sleep(2000); // Dê um tempo para o Inventor atualizar seu estado
                        Console.WriteLine("  Instância agora visível.");
                    }
                    else
                    {
                        Console.WriteLine("  Instância ativa do Inventor já está visível.");
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Falha ao conectar à instância ativa (erro: {ex.Message}).");
            }

            // Se não conseguiu conectar a uma instância ativa ou a instância não era adequada, cria uma nova.
            try
            {
                Console.WriteLine("Nenhuma instância ativa adequada encontrada ou falha ao conectar. Criando nova instância...");
                Type? inventorType = Type.GetTypeFromProgID("Inventor.Application");
                if (inventorType == null)
                {
                    throw new InvalidOperationException("Inventor não está instalado ou ProgID inválido.");
                }

                _inventorApp = Activator.CreateInstance(inventorType);
                if (_inventorApp == null)
                {
                    throw new InvalidOperationException("Não foi possível criar uma nova instância do Inventor.");
                }

                _inventorApp.Visible = true;
                System.Threading.Thread.Sleep(5000); // Dê mais tempo para a nova instância carregar completamente

                string versao = _inventorApp.SoftwareVersion.DisplayVersion;
                Console.WriteLine($"✓ Nova instância criada - Inventor {versao}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Falha total ao conectar ou criar instância do Inventor: {ex.Message}");
            }
            if (_inventorApp != null)
        {
            Console.WriteLine($"✓ Inventor conectado - Versão: {GetInventorVersion()}");
        }
        }

        public List<AssemblyInfo> ListAvailableAssemblies()
        {
            var assemblies = new List<AssemblyInfo>();

            if (_inventorApp == null) return assemblies;

            try
            {
                dynamic documents = _inventorApp.Documents;
                int count = documents.Count;
                Console.WriteLine($"📄 Total de documentos abertos na instância conectada: {count}"); // Log mais descritivo

                // Determina qual é o documento ativo
                dynamic? activeDoc = null;
                try
                {
                    activeDoc = _inventorApp.ActiveDocument;
                }
                catch
                {
                    Console.WriteLine("⚠️ Nenhum documento ativo detectado na instância conectada.");
                }

                if (count > 0)
                {
                    for (int i = 1; i <= count; i++)
                    {
                        try
                        {
                            dynamic doc = documents.Item[i];
                            int docType = doc.DocumentType;

                            // *** CORREÇÃO APLICADA AQUI (MANTIDA) ***
                            // Só adiciona montagens (tipo 12291 - kAssemblyDocumentObject)
                            if (docType == 12291)
                            {
                                string displayName = doc.DisplayName?.ToString() ?? "Nome não disponível";
                                string fullPath = "";
                                bool isActive = false;

                                try
                                {
                                    fullPath = doc.FullFileName?.ToString() ?? "Caminho não disponível";
                                }
                                catch { /* Ignora erro se o caminho não estiver disponível */ }

                                // Verifica se é o documento ativo
                                try
                                {
                                    if (activeDoc != null)
                                    {
                                        string activeDocPath = activeDoc.FullFileName?.ToString() ?? "";
                                        isActive = !string.IsNullOrEmpty(activeDocPath) && activeDocPath.Equals(fullPath, StringComparison.OrdinalIgnoreCase);
                                    }
                                }
                                catch { /* Ignora erro se o documento ativo não puder ser verificado */ }

                                assemblies.Add(new AssemblyInfo
                                {
                                    DisplayName = displayName,
                                    FullPath = fullPath,
                                    IsActive = isActive,
                                    Document = doc
                                });

                                Console.WriteLine($"  ✓ Montagem encontrada: {displayName} {(isActive ? "[ATIVA]" : "")}");
                            }
                            else
                            {
                                // Log para outros tipos de documentos, apenas para depuração
                                Console.WriteLine($"  🔍 Documento encontrado, mas não é montagem (Tipo: {docType}): {doc.DisplayName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  ⚠️ Erro ao processar documento {i}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"📦 Total de montagens encontradas na instância conectada: {assemblies.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao listar documentos: {ex.Message}");
            }

            return assemblies;
        }

        public List<BomItem> GetBOMFromDocument(dynamic assemblyDoc)
        {
            try
            {
                Console.WriteLine($"Processando documento: {assemblyDoc.DisplayName}");
                return ExtractBOM(assemblyDoc);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao processar documento: {ex.Message}", ex);
            }
        }

        public List<BomItem> GetBOMFromFile(string assemblyFilePath)
        {
            if (_inventorApp == null)
            {
                throw new InvalidOperationException("Não conectado ao Inventor.");
            }

            dynamic? doc = null;
            try
            {
                Console.WriteLine($"Abrindo arquivo: {assemblyFilePath}");
                // O segundo parâmetro 'false' significa que o documento não será visível no Inventor
                // Mantenha como false para abrir em segundo plano
                doc = _inventorApp.Documents.Open(assemblyFilePath, false);

                int docType = doc.DocumentType;
                Console.WriteLine($"Tipo do arquivo aberto: {docType}");

                // *** CORREÇÃO APLICADA AQUI (MANTIDA) ***
                // kAssemblyDocumentObject = 12291
                // kPartDocumentObject = 12290
                if (docType != 12291) // Se NÃO for explicitamente uma montagem
                {
                    // Tenta detectar como montagem ou sub-montagem via BOM
                    // Isso é útil para lidar com arquivos de peça que podem ter uma "lista de materiais" interna
                    try
                    {
                        dynamic compDef = doc.ComponentDefinition;
                        dynamic bom = compDef.BOM;
                        Console.WriteLine("✓ Detectado como montagem/sub-montagem via BOM (pode ser uma peça com BOM)");
                    }
                    catch
                    {
                        // Se não for montagem e não tiver BOM, então não é adequado para extração
                        throw new InvalidOperationException($"Arquivo '{Path.GetFileName(assemblyFilePath)}' não é uma montagem válida para extração de BOM. Tipo detectado: {docType}.");
                    }
                }
                else
                {
                    Console.WriteLine("✓ Confirmado como montagem (.iam)");
                }

                return ExtractBOM(doc);
            }
            finally
            {
                // Sempre tenta fechar o documento que foi aberto pelo programa,
                // para evitar deixar arquivos abertos no Inventor desnecessariamente.
                try
                {
                    // true = salvar alterações, false = descartar alterações
                    // Mude para 'false' se não quiser que o programa salve nada ao fechar
                    doc?.Close(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro ao fechar o documento '{Path.GetFileName(assemblyFilePath)}': {ex.Message}");
                }
            }
        }

        private List<BomItem> ExtractBOM(dynamic assemblyDoc)
        {
            var bomItems = new List<BomItem>();

            try
            {
                Console.WriteLine("Acessando ComponentDefinition...");
                dynamic compDef = assemblyDoc.ComponentDefinition;

                Console.WriteLine("Acessando BOM...");
                dynamic bom = compDef.BOM;

                Console.WriteLine("Configurando BOM estruturado...");
                bom.StructuredViewEnabled = true;
                bom.StructuredViewFirstLevelOnly = false; // Garante que todos os níveis sejam extraídos

                Console.WriteLine("Obtendo vista estruturada...");
                dynamic bomView = bom.BOMViews["Structured"];

                int rowCount = bomView.BOMRows.Count;
                Console.WriteLine($"BOM estruturado obtido! Processando {rowCount} itens principais...");

                ProcessBOMRows(bomView.BOMRows, bomItems, 0); // Inicia o processamento recursivo

                Console.WriteLine($"✓ {bomItems.Count} itens extraídos no total!");
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro ao extrair BOM: {ex.Message}", ex);
            }

            return bomItems;
        }

        private void ProcessBOMRows(dynamic bomRows, List<BomItem> bomItems, int level)
        {
            foreach (dynamic bomRow in bomRows)
            {
                try
                {
                    // Acessa o ComponentOccurrence associado à linha do BOM
                    dynamic? componentOccurrence = null;
                    try { componentOccurrence = bomRow.ComponentOccurrence; }
                    catch { /* Pode não ter ocorrência para linhas de nível superior ou virtual */ }

                    // Acessa o ComponentDefinition através da ocorrência, ou diretamente da linha do BOM se não houver ocorrência
                    dynamic? componentDefinition = null;
                    if (componentOccurrence != null)
                    {
                        componentDefinition = componentOccurrence.Definition;
                    }
                    else
                    {
                        // Fallback: Tenta pegar o ComponentDefinition diretamente da linha do BOM (para itens "virtuais" ou BOMs de nível superior)
                        try { componentDefinition = bomRow.ComponentDefinitions[1]; }
                        catch { /* Ignora se não conseguir */ }
                    }

                    // Se não conseguir um componentDefinition, este item não é um componente válido para extração detalhada
                    if (componentDefinition == null)
                    {
                        Console.WriteLine($"⚠️ Ignorando item sem ComponentDefinition válido: {GetSafeString(() => bomRow.ItemNumber?.ToString())} (Nível: {level})");
                        continue; // Pula este item e vai para o próximo
                    }

                    // Extrai as propriedades
                    string partNumber = GetPartNumber(bomRow, componentDefinition);
                    string description = GetDescription(bomRow, componentDefinition);
                    string documentPath = GetDocumentPath(componentDefinition);
                    string material = GetMaterial(componentDefinition);
                    double mass = GetMass(componentDefinition);
                    double volume = GetVolume(componentDefinition);

                    var bomItem = new BomItem
                    {
                        Level = level,
                        ItemNumber = GetSafeString(() => bomRow.ItemNumber?.ToString()),
                        PartNumber = partNumber,
                        Description = description,
                        Quantity = GetSafeValue(() => bomRow.ItemQuantity),
                        Units = GetSafeString(() => bomRow.ItemQuantityUnits?.ToString()),
                        DocumentPath = documentPath,
                        Material = material,
                        Mass = mass,
                        Volume = volume
                    };

                    bomItems.Add(bomItem);

                    // Log com mais detalhes
                    string indent = new string(' ', level * 2);
                    Console.WriteLine($"{indent}✓ {bomItem.PartNumber} (Qtd: {bomItem.Quantity}) - {bomItem.Description}");

                    // Processa filhos recursivamente
                    try
                    {
                        if (bomRow.ChildRows != null && bomRow.ChildRows.Count > 0)
                        {
                            ProcessBOMRows(bomRow.ChildRows, bomItems, level + 1);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{indent}⚠️ Erro ao processar filhos para {bomItem.PartNumber}: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro geral no item nível {level}: {ex.Message}");

                    // Adiciona um item de erro ao BOM para rastreamento
                    bomItems.Add(new BomItem
                    {
                        Level = level,
                        ItemNumber = "ERRO",
                        PartNumber = "ERRO",
                        Description = $"Erro ao processar item: {ex.Message}",
                        Quantity = 0
                    });
                }
            }
        }

        // Métodos auxiliares para extração segura de propriedades

        private string GetSafeString(Func<string?> getter)
        {
            try { return getter() ?? ""; }
            catch { return ""; }
        }

        private object GetSafeValue(Func<object> getter)
        {
            try { return getter() ?? 0; }
            catch { return 0; }
        }

        private string GetPartNumber(dynamic bomRow, dynamic componentDefinition)
        {
            try
            {
                // Tenta obter do PropertySet "Design Tracking Properties"
                dynamic designProps = componentDefinition.Document.PropertySets["Design Tracking Properties"];
                string? partNumber = designProps["Part Number"].Value?.ToString();
                if (!string.IsNullOrEmpty(partNumber))
                    return partNumber;
            }
            catch { /* Ignora e tenta fallback */ }

            // Fallback para nome do arquivo sem extensão
            try
            {
                string? fileName = componentDefinition.Document.DisplayName?.ToString();
                return !string.IsNullOrEmpty(fileName) ? Path.GetFileNameWithoutExtension(fileName) : "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private string GetDescription(dynamic bomRow, dynamic componentDefinition)
        {
            try
            {
                dynamic designProps = componentDefinition.Document.PropertySets["Design Tracking Properties"];
                return designProps["Description"].Value?.ToString() ?? "Sem descrição";
            }
            catch
            {
                return "Sem descrição";
            }
        }

        private string GetDocumentPath(dynamic componentDefinition)
        {
            try
            {
                return componentDefinition.Document.FullFileName?.ToString() ?? "N/A";
            }
            catch
            {
                return "N/A";
            }
        }

        private string GetMaterial(dynamic componentDefinition)
        {
            try
            {
                // kPartDocumentObject = 12290
                if (componentDefinition.Document.DocumentType == 12290) // Se for uma peça
                {
                    dynamic material = componentDefinition.Material; // Acessa o material da definição do componente
                    return material?.DisplayName?.ToString() ?? "Material não definido";
                }
                return "N/A"; // Não aplica material para montagens ou outros tipos
            }
            catch
            {
                return "N/A";
            }
        }

        private double GetMass(dynamic componentDefinition)
        {
            try
            {
                // A massa está nas propriedades de massa da definição do componente
                return Convert.ToDouble(componentDefinition.MassProperties.Mass);
            }
            catch
            {
                return 0.0;
            }
        }

        private double GetVolume(dynamic componentDefinition)
        {
            try
            {
                // O volume está nas propriedades de massa da definição do componente
                return Convert.ToDouble(componentDefinition.MassProperties.Volume);
            }
            catch
            {
                return 0.0;
            }
        }

        // Métodos de exportação (não foram alterados)

        public void ExportBOMToCSV(List<BomItem> bomItems, string filePath)
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);

            writer.WriteLine("Nível,Item,Peça,Descrição,Quantidade,Unidades,Material,Massa,Volume,Caminho");

            foreach (var item in bomItems)
            {
                // Removi a indentação no CSV para a coluna Item, pois o Level já indica a hierarquia.
                // Se quiser de volta, adicione: string indent = new string('-', item.Level * 2);
                writer.WriteLine($"{item.Level}," +
                               $"\"{item.ItemNumber}\"," + // Removida indentação no CSV
                               $"\"{item.PartNumber}\"," +
                               $"\"{item.Description}\"," +
                               $"{item.Quantity}," +
                               $"\"{item.Units}\"," +
                               $"\"{item.Material}\"," +
                               $"{item.Mass:F3}," +
                               $"{item.Volume:F6}," +
                               $"\"{item.DocumentPath}\"");
            }
        }

        public void ExportBOMToExcel(List<BomItem> bomItems, string filePath)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("BOM");

            // Cabeçalhos
            var headers = new[] { "Nível", "Item", "Peça", "Descrição", "Qtd", "Unidades", "Material", "Massa(kg)", "Volume(cm³)", "Arquivo" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
            }

            // Formato cabeçalho
            using (var range = worksheet.Cells[1, 1, 1, headers.Length])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }

            // Dados
            for (int i = 0; i < bomItems.Count; i++)
            {
                var item = bomItems[i];
                int row = i + 2;

                worksheet.Cells[row, 1].Value = item.Level;
                worksheet.Cells[row, 2].Value = item.ItemNumber;
                worksheet.Cells[row, 3].Value = item.PartNumber;
                worksheet.Cells[row, 4].Value = item.Description;
                worksheet.Cells[row, 5].Value = item.Quantity;
                worksheet.Cells[row, 6].Value = item.Units;
                worksheet.Cells[row, 7].Value = item.Material;
                worksheet.Cells[row, 8].Value = item.Mass;
                worksheet.Cells[row, 9].Value = item.Volume;
                worksheet.Cells[row, 10].Value = item.DocumentPath;

                // Indentação para a coluna "Item" no Excel
                worksheet.Cells[row, 2].Style.Indent = item.Level;

                // Cor alternada
                if (i % 2 == 0)
                {
                    using (var range = worksheet.Cells[row, 1, row, headers.Length])
                    {
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                }
            }

            worksheet.Cells.AutoFitColumns();

            // Informações extras
            worksheet.Cells[bomItems.Count + 3, 1].Value = "Gerado em:";
            worksheet.Cells[bomItems.Count + 3, 2].Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            worksheet.Cells[bomItems.Count + 4, 1].Value = "Total:";
            worksheet.Cells[bomItems.Count + 4, 2].Value = bomItems.Count;

            package.SaveAs(new FileInfo(filePath));
        }
        public string? GetInventorVersion()
        {
            try
            {
                if (_inventorApp != null)
                {
                    return _inventorApp.SoftwareVersion.DisplayVersion?.ToString();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

    public dynamic? GetInventorApp()
    {
        try
        {
            // Retorna a aplicação Inventor para subscrição de eventos
            if (_inventorApp != null)
            {
                return _inventorApp;
            }
            
            // Se não existe, tenta conectar
            ConnectToInventor();
            return _inventorApp;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao obter aplicação Inventor: {ex.Message}");
            return null;
        }
    }

    // ✅ ADICIONAR - Verifica se aplicação está válida
    public bool IsInventorAppValid()
    {
        try
        {
            if (_inventorApp == null) return false;
            
            // Testa acesso simples para verificar se ainda está válida
            var version = _inventorApp.SoftwareVersion;
            return version != null;
        }
        catch
        {
            return false;
        }
    }

    }

    public class BomItem
    {
        public int Level { get; set; }
        public string ItemNumber { get; set; } = "";
        public string PartNumber { get; set; } = "";
        public string Description { get; set; } = "";
        public object Quantity { get; set; } = 0; // Usar 'object' para lidar com Int32 ou Double
        public string Units { get; set; } = "";
        public string DocumentPath { get; set; } = "";
        public string Material { get; set; } = "";
        public double Mass { get; set; }
        public double Volume { get; set; }
    }
}
