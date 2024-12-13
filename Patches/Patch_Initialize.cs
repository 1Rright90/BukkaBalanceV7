using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using YSBCaptain.Core.Logging;
using YSBCaptain.Performance;
using Formation = TaleWorlds.MountAndBlade.Formation;
using Mission = TaleWorlds.MountAndBlade.Mission;

namespace YSBCaptain.Patches
{
    /// <summary>
    /// Harmony patch for Team initialization to ensure proper setup of formations and order controllers
    /// Implements performance monitoring and resource management for team initialization
    /// </summary>
    [HarmonyPatch(typeof(Team), "Initialize")]
    public class Patch_Initialize
    {
        private static readonly Dictionary<string, FieldInfo> CachedFields = new Dictionary<string, FieldInfo>();
        private static readonly object CacheLock = new object();
        private static readonly PerformanceMonitor _performanceMonitor = new PerformanceMonitor("TeamInitialization");
        
        /// <summary>
        /// Gets a cached field info to improve reflection performance
        /// </summary>
        private static FieldInfo GetCachedField(string fieldName)
        {
            using (_performanceMonitor.MeasureScope("FieldInfoRetrieval"))
            {
                lock (CacheLock)
                {
                    if (!CachedFields.TryGetValue(fieldName, out var field))
                    {
                        field = AccessTools.Field(typeof(Team), fieldName);
                        if (field != null)
                        {
                            CachedFields[fieldName] = field;
                            _performanceMonitor.RecordMetric("CachedFields", CachedFields.Count);
                        }
                    }
                    return field;
                }
            }
        }

        /// <summary>
        /// Prefix patch for Team initialization
        /// Handles setup of formations, agents, and order controllers with performance monitoring
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(Team __instance)
        {
            using (_performanceMonitor.MeasureScope())
            {
                try 
                {
                    // Get required fields using cached reflection
                    var fields = new Dictionary<string, FieldInfo>
                    {
                        {"_activeAgents", GetCachedField("_activeAgents")},
                        {"_teamAgents", GetCachedField("_teamAgents")},
                        {"_cachedEnemyDataForFleeing", GetCachedField("_cachedEnemyDataForFleeing")},
                        {"_formationsIncludingSpecialAndEmpty", GetCachedField("_formationsIncludingSpecialAndEmpty")},
                        {"_formationsIncludingEmpty", GetCachedField("_formationsIncludingEmpty")},
                        {"_querySystem", GetCachedField("_querySystem")},
                        {"_detachmentManager", GetCachedField("_detachmentManager")},
                        {"_orderControllers", GetCachedField("_orderControllers")}
                    };

                    // Validate all fields are found
                    foreach (var kvp in fields)
                    {
                        if (kvp.Value == null)
                        {
                            Logger.LogError($"Required field {kvp.Key} not found in Team class");
                            _performanceMonitor.RecordError("FieldValidation", new MissingFieldException($"Field {kvp.Key} not found"));
                            return true;
                        }
                    }

                    // Initialize basic lists with performance monitoring
                    using (_performanceMonitor.MeasureScope("BasicListInitialization"))
                    {
                        fields["_activeAgents"].SetValue(__instance, new MBList<Agent>());
                        fields["_teamAgents"].SetValue(__instance, new MBList<Agent>());
                        fields["_cachedEnemyDataForFleeing"].SetValue(__instance, 
                            new MBList<ValueTuple<float, WorldPosition, int, Vec2, Vec2, bool>>());
                    }

                    if (!GameNetwork.IsReplay)
                    {
                        using (_performanceMonitor.MeasureScope("FormationInitialization"))
                        {
                            // Initialize formations with predefined capacity
                            const int FormationCapacity = 100;
                            var formationList1 = new MBList<Formation>(FormationCapacity);
                            var formationList2 = new MBList<Formation>(FormationCapacity);
                            
                            fields["_formationsIncludingSpecialAndEmpty"].SetValue(__instance, formationList1);
                            fields["_formationsIncludingEmpty"].SetValue(__instance, formationList2);

                            // Use a static handler to avoid delegate allocation
                            Action<Formation> handler = FormationBehaviorChanged;
                            formationList1.OnItemAddedOrRemoved = handler;
                            formationList2.OnItemAddedOrRemoved = handler;
                        }

                        if (__instance.Mission != null)
                        {
                            using (_performanceMonitor.MeasureScope("OrderControllerSetup"))
                            {
                                List<OrderController> orderControllerList = new List<OrderController>();
                                fields["_orderControllers"].SetValue(__instance, orderControllerList);

                                // Create a single delegate for order issued events that matches OnOrderIssuedDelegate signature
                                OnOrderIssuedDelegate orderIssuedHandler = 
                                    (orderType, appliedFormations, orderControllerParam, delegateParams) =>
                                    {
                                        MethodInfo methodInfo = AccessTools.Method(typeof(Team), "OrderController_OnOrderIssued", null, null);
                                        methodInfo?.Invoke(__instance, new object[] 
                                        { 
                                            orderType, 
                                            appliedFormations, 
                                            orderControllerParam, 
                                            delegateParams 
                                        });
                                    };

                                // Initialize order controllers
                                OrderController orderController1 = new OrderController(__instance.Mission, __instance, null);
                                orderControllerList.Add(orderController1);
                                orderController1.OnOrderIssued += orderIssuedHandler;
                                
                                OrderController orderController2 = new OrderController(__instance.Mission, __instance, null);
                                orderControllerList.Add(orderController2);
                                orderController2.OnOrderIssued += orderIssuedHandler;

                                _performanceMonitor.RecordMetric("OrderControllersInitialized", orderControllerList.Count);
                            }
                        }

                        using (_performanceMonitor.MeasureScope("SystemInitialization"))
                        {
                            fields["_querySystem"].SetValue(__instance, new TeamQuerySystem(__instance));
                            fields["_detachmentManager"].SetValue(__instance, new DetachmentManager(__instance));
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Critical error in Team.Initialize patch: {ex.Message}", ex);
                    _performanceMonitor.RecordError("TeamInitialization", ex);
                    return true;
                }
            }
        }

        /// <summary>
        /// Handles formation behavior changes and updates query system accordingly
        /// </summary>
        private static void FormationBehaviorChanged(Formation formation)
        {
            using (_performanceMonitor.MeasureScope("FormationBehaviorChange"))
            {
                try
                {
                    if (formation?.QuerySystem != null)
                    {
                        formation.QuerySystem.Expire();
                        _performanceMonitor.RecordMetric("FormationBehaviorChanges", 1);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error in formation behavior change: {ex.Message}", ex);
                    _performanceMonitor.RecordError("FormationBehavior", ex);
                }
            }
        }
    }
}
