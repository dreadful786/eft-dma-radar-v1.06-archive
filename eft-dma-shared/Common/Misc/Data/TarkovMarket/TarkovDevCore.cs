﻿using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using System.Text;
using System.IO;
using System.Text.Json.Serialization;

namespace eft_dma_shared.Common.Misc.Data.TarkovMarket
{
    internal static class TarkovDevCore
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Query the Tarkov.dev API for game data with specified game mode
        /// </summary>
        /// <param name="isPve">True for PvE mode, False for PvP (regular) mode</param>
        /// <returns>Query result from Tarkov.dev API</returns>
        public static async Task<TarkovDevQuery> QueryTarkovDevAsync(bool isPve = true)
        {
            string gameMode = isPve ? "pve" : "regular";

            // Create the GraphQL query with proper interpolation
            string queryText = $$"""
            {
                items(gameMode:{{gameMode}}) { 
                    id 
                    name 
                    shortName 
                    width 
                    height 
                    sellFor { 
                        vendor { 
                            name 
                        } 
                        priceRUB 
                    } 
                    basePrice 
                    avg24hPrice 
                    historicalPrices { 
                        price 
                    } 
                    categories { 
                        name 
                    } 
                }
                questItems { 
                    id shortName 
                }
                lootContainers { 
                    id 
                    normalizedName 
                    name 
                }
                tasks {
                    id
                    name
                    objectives {
                        id
                        type
                        description
                        maps {
                            id
                            name
                            normalizedName
                        }
                        ... on TaskObjectiveItem {
                            item {
                            id
                            name
                            shortName
                            }
                            zones {
                            id
                            map {
                                id
                                normalizedName
                                name
                            }
                            position {
                                y
                                x
                                z
                            }
                            }
                            requiredKeys {
                            id
                            name
                            shortName
                            }
                            count
                            foundInRaid
                        }
                        ... on TaskObjectiveMark {
                            id
                            description
                            markerItem {
                            id
                            name
                            shortName
                            }
                            maps {
                            id
                            normalizedName
                            name
                            }
                            zones {
                            id
                            map {
                                id
                                normalizedName
                                name
                            }
                            position {
                                y
                                x
                                z
                            }
                            }
                            requiredKeys {
                            id
                            name
                            shortName
                            }
                        }
                        ... on TaskObjectiveQuestItem {
                            id
                            description
                            requiredKeys {
                            id
                            name
                            shortName
                            }
                            maps {
                            id
                            normalizedName
                            name
                            }
                            zones {
                            id
                            map {
                                id
                                normalizedName
                                name
                            }
                            position {
                                y
                                x
                                z
                            }
                            }
                            requiredKeys {
                            id
                            name
                            shortName
                            }
                            questItem {
                                id
                                name
                                shortName
                                normalizedName
                                description
                            }
                            count
                        }
                        ... on TaskObjectiveBasic {
                            id
                            description
                            requiredKeys {
                            id
                            name
                            shortName
                            }
                            maps {
                            id
                            normalizedName
                            name
                            }
                            zones {
                            id
                            map {
                                id
                                normalizedName
                                name
                            }
                            position {
                                y
                                x
                                z
                            }
                            }
                            requiredKeys {
                            id
                            name
                            shortName
                            }
                        }
                    }
                }
            }
            """;

            var query = new Dictionary<string, string>()
            {
                { "query", queryText }
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var response = await SharedProgram.HttpClient.PostAsJsonAsync(
                requestUri: "https://api.tarkov.dev/graphql",
                value: query,
                cancellationToken: cts.Token);
            response.EnsureSuccessStatusCode();
            return await JsonSerializer.DeserializeAsync<TarkovDevQuery>(await response.Content.ReadAsStreamAsync(), _jsonOptions);
        }


        /// <summary>
        /// Compares default_data.json with data.json and adds any missing items to data.json
        /// </summary>
        /// <returns>Task representing the operation</returns>
        public static async Task MergeDefaultDataWithDataJsonAsync()
        {
            string dataFilePath = Path.Combine(SharedProgram.ConfigPath.FullName, "data.json");

            // Check if data.json exists
            if (!File.Exists(dataFilePath))
            {
                return; // Nothing to merge if data.json doesn't exist
            }

            try
            {
                // Load default data
                string defaultJson = await GetDefaultDataAsync();
                var jsonOptions = new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                var defaultData = JsonSerializer.Deserialize<EftDataManager.TarkovMarketData>(defaultJson, jsonOptions);

                // Load current data.json
                string dataJson = await File.ReadAllTextAsync(dataFilePath);
                var currentData = JsonSerializer.Deserialize<EftDataManager.TarkovMarketData>(dataJson, jsonOptions);

                bool hasChanges = false;

                // Create dictionary of existing items by BsgId for quick lookup
                var existingItemsDict = currentData.Items.ToDictionary(i => i.BsgId, StringComparer.OrdinalIgnoreCase);

                // Find missing items from default data
                foreach (var defaultItem in defaultData.Items)
                {
                    if (!existingItemsDict.ContainsKey(defaultItem.BsgId))
                    {
                        // Add missing item to current data
                        currentData.Items.Add(defaultItem);
                        hasChanges = true;
                    }
                }

                // Create dictionary of existing tasks by Id for quick lookup
                var existingTasksDict = currentData.Tasks?.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase)
                    ?? new Dictionary<string, EftDataManager.TaskElement>(StringComparer.OrdinalIgnoreCase);

                // Check if Tasks collection exists, if not, initialize it
                if (currentData.Tasks == null)
                {
                    currentData.Tasks = new List<EftDataManager.TaskElement>();
                }

                // Find missing tasks from default data
                if (defaultData.Tasks != null)
                {
                    foreach (var defaultTask in defaultData.Tasks)
                    {
                        if (!existingTasksDict.ContainsKey(defaultTask.Id))
                        {
                            // Add missing task to current data
                            currentData.Tasks.Add(defaultTask);
                            hasChanges = true;
                        }
                        else
                        {
                            // Task exists, process objectives
                            var currentTask = existingTasksDict[defaultTask.Id];

                            if (defaultTask.Objectives != null && currentTask.Objectives != null)
                            {
                                // Create a dictionary of objectives by ID for easier lookup
                                var currentObjectivesDict = new Dictionary<string, EftDataManager.TaskElement.ObjectiveElement>(StringComparer.OrdinalIgnoreCase);
                                foreach (var obj in currentTask.Objectives)
                                {
                                    if (obj?.Id != null)
                                    {
                                        currentObjectivesDict[obj.Id] = obj;
                                    }
                                }

                                // Process each objective in the default task
                                foreach (var defaultObjective in defaultTask.Objectives)
                                {
                                    if (defaultObjective?.Id == null) continue;

                                    // Check if the objective exists in current data
                                    if (currentObjectivesDict.TryGetValue(defaultObjective.Id, out var currentObjective))
                                    {
                                        // Check if zones need to be preserved
                                        if (defaultObjective.Zones != null && defaultObjective.Zones.Count > 0)
                                        {
                                            // If current objective has no zones, copy from default
                                            if (currentObjective.Zones == null || currentObjective.Zones.Count == 0)
                                            {
                                                currentObjective.Zones = defaultObjective.Zones;
                                                hasChanges = true;
                                            }
                                            else
                                            {
                                                // Both have zones - create a dictionary of current zones by ID
                                                var currentZonesDict = new Dictionary<string, EftDataManager.TaskElement.ObjectiveElement.ZoneElement>(StringComparer.OrdinalIgnoreCase);
                                                foreach (var zone in currentObjective.Zones)
                                                {
                                                    if (zone?.Id != null)
                                                    {
                                                        currentZonesDict[zone.Id] = zone;
                                                    }
                                                }

                                                // Check each default zone
                                                foreach (var defaultZone in defaultObjective.Zones)
                                                {
                                                    if (defaultZone?.Id == null) continue;

                                                    // Add missing zones
                                                    if (!currentZonesDict.ContainsKey(defaultZone.Id))
                                                    {
                                                        currentObjective.Zones.Add(defaultZone);
                                                        hasChanges = true;
                                                    }
                                                    else
                                                    {
                                                        var currentZone = currentZonesDict[defaultZone.Id];

                                                        // Ensure zone outline is preserved
                                                        if (defaultZone.Outline != null && defaultZone.Outline.Count > 0 &&
                                                            (currentZone.Outline == null || currentZone.Outline.Count == 0))
                                                        {
                                                            currentZone.Outline = defaultZone.Outline;
                                                            hasChanges = true;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Objective doesn't exist in current task, add it
                                        currentTask.Objectives.Add(defaultObjective);
                                        hasChanges = true;
                                    }
                                }
                            }
                        }
                    }
                }

                // Save updated data if there were changes
                if (hasChanges)
                {
                    var outputJson = JsonSerializer.Serialize(currentData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    await File.WriteAllTextAsync(dataFilePath, outputJson);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw since this is a non-critical operation
                Console.WriteLine($"Error merging default data with data.json: {ex}");
            }
        }

        /// <summary>
        /// Gets the embedded default data JSON
        /// </summary>
        private static async Task<string> GetDefaultDataAsync()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("eft-dma-shared.DEFAULT_DATA.json"))
            {
                var data = new byte[stream!.Length];
                await stream.ReadExactlyAsync(data);
                return Encoding.UTF8.GetString(data);
            }
        }
    }
}
