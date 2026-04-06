using System.Collections;
using BepInEx;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Networking;

namespace SuperLandmine.Patchs

{
    [HarmonyPatch(typeof(Landmine))]
    public class SuperLandminePatch
    {
        [HarmonyPatch("PressMineClientRpc")]
        [HarmonyPostfix]
        public static void playTriggerSound(Landmine __instance)
        {
            Plugin.log.LogInfo("Play trigger sound");
            __instance.mineAudio.volume = 1.3f;
            __instance.mineFarAudio.volume = 1.3f;
            Traverse.Create(__instance).Field("pressMineDebounceTimer").SetValue(0.5f);
            __instance.mineAudio.PlayOneShot(__instance.minePress, 1.3f);
            WalkieTalkie.TransmitOneShotAudio(__instance.mineAudio, __instance.minePress);
        }
        [HarmonyPatch("Detonate")]
        [HarmonyPrefix]
        public static void IncreaseAudioVolume(Landmine __instance)
        {
            Plugin.log.LogInfo("Increase landmine volume and kill range");
            __instance.StartCoroutine(DelayedActions(__instance));
            return;
        }
        private static IEnumerator DelayedActions(Landmine __instance)
        {
            yield return new WaitForSeconds(0.5f);
            __instance.mineAudio.volume = 1.2f;
            __instance.mineFarAudio.volume = 1.3f;
            __instance.mineAudio.pitch = Random.Range(0.93f, 1.07f);
            __instance.mineAudio.PlayOneShot(__instance.mineDetonate, 1.2f);
            Landmine.SpawnExplosion(__instance.transform.position + Vector3.up, spawnExplosionEffect: true, 10.0f, 12.0f);
            yield return new WaitForSeconds(0.5f);

        }
        [HarmonyPatch("Detonate")]
        [HarmonyPostfix]
        public static void disableSoundAfterExplode(Landmine __instance)
        {
            if (Plugin.config_EnableLandmineSound.Value)
            {
                __instance.mineAudio.volume = 0f;
                __instance.mineFarAudio.volume = 0f;
            }
        }


        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        public static void enabledLandMineSound(Landmine __instance)
        {
            if (Plugin.config_EnableLandmineSound.Value)
            {
                __instance.mineAudio.volume = 0f;
                __instance.mineFarAudio.volume = 0f;
            }

        }


        [HarmonyPatch("OnTriggerEnter")]
        [HarmonyPrefix]
        public static void anyObjectTriggerLandmineEnter(ref Collider other, Landmine __instance)
        {
            if (Plugin.config_EnemyCanTriggerLandmine.Value == true)
            {
                float pressMineDebounceTimer = Traverse.Create(__instance).Field("pressMineDebounceTimer").GetValue<float>();

                if (__instance.hasExploded || pressMineDebounceTimer > 0f)
                {
                    return;
                }
                if (other.CompareTag("Player"))
                {
                    PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
                    if (!(component != GameNetworkManager.Instance.localPlayerController) && component != null && !component.isPlayerDead)
                    {
                        Traverse.Create(__instance).Field("localPlayerOnMine").SetValue(true);
                        Traverse.Create(__instance).Field("pressMineDebounceTimer").SetValue(0.5f);
                        __instance.PressMineServerRpc();
                    }
                }
                else
                {
                    if (!other.CompareTag("PlayerRagdoll") && !other.CompareTag("PhysicsProp") && !other.CompareTag("Enemy"))
                    {
                        return;
                    }
                    if ((bool)other.GetComponent<DeadBodyInfo>())
                    {
                        if (other.GetComponent<DeadBodyInfo>().playerScript != GameNetworkManager.Instance.localPlayerController)
                        {
                            return;
                        }
                    }
                    else if ((bool)other.GetComponent<GrabbableObject>() && !other.GetComponent<GrabbableObject>().NetworkObject.IsOwner)
                    {
                        return;
                    }
                    Traverse.Create(__instance).Field("pressMineDebounceTimer").SetValue(0.5f);

                    __instance.PressMineServerRpc();
                }
                return;
            }

        }

        [HarmonyPatch("OnTriggerExit")]
        [HarmonyPrefix]
        public static void anyObjectTriggerLandmineExit(ref Collider other, Landmine __instance)
        {
            if (Plugin.config_EnemyCanTriggerLandmine.Value == true)
            {
                bool mineActivated = Traverse.Create(__instance).Field("mineActivated").GetValue<bool>();

                if (__instance.hasExploded || !mineActivated)
                {
                    return;
                }
                if (other.CompareTag("Player"))
                {
                    PlayerControllerB component = other.gameObject.GetComponent<PlayerControllerB>();
                    if (component != null && !component.isPlayerDead && !(component != GameNetworkManager.Instance.localPlayerController))
                    {
                        Traverse.Create(__instance).Field("localPlayerOnMine").SetValue(false);
                        Traverse.Create(__instance).Method("TriggerMineOnLocalClientByExiting").GetValue();
                    }
                }
                else
                {
                    if (!other.CompareTag("PlayerRagdoll") && !other.CompareTag("PhysicsProp") && !other.CompareTag("Enemy"))
                    {
                        return;
                    }
                    if ((bool)other.GetComponent<DeadBodyInfo>())
                    {
                        if (other.GetComponent<DeadBodyInfo>().playerScript != GameNetworkManager.Instance.localPlayerController)
                        {
                            return;
                        }
                    }
                    else if ((bool)other.GetComponent<GrabbableObject>() && !other.GetComponent<GrabbableObject>().NetworkObject.IsOwner)
                    {
                        return;
                    }
                    Traverse.Create(__instance).Method("TriggerMineOnLocalClientByExiting").GetValue();
                }

                return;
            }

        }

    }

    [HarmonyPatch(typeof(RoundManager))]
    public class RoundManagerPatch
    {
        [HarmonyPatch("LoadNewLevel")]
        [HarmonyPrefix]
        public static void spawnLanmineInside(ref SelectableLevel newLevel)
        {
            if (Plugin.config_UseDefaultLandmineSpawnRate.Value == true)
            {
                return;
            }
            Plugin.log.LogInfo("Load landmine");
            SelectableLevel selectableLevel = newLevel;
            SpawnableMapObject[] spawnableMapObjects = selectableLevel.spawnableMapObjects;
            if (selectableLevel.spawnableMapObjects.Length != 0)
            {
                Plugin.log.LogInfo("Spawn landmine inside");
                foreach (SpawnableMapObject spawnObject in spawnableMapObjects)
                {
                    if ((Object)(object)spawnObject.prefabToSpawn.GetComponentInChildren<Landmine>() != (Object)null)
                    {

                        spawnObject.numberToSpawn = new AnimationCurve((Keyframe[])(object)new Keyframe[2]
                        {
                                new Keyframe(0f, Plugin.config_LandmineMinAmount.Value),
                                new Keyframe(1f, Plugin.config_LandmineMaxAmount.Value)
                        });
                    }
                }
            }
        }
        [HarmonyPatch("SpawnOutsideHazards")]
        [HarmonyPrefix]
        public static void spawnLandmineOutside(RoundManager __instance)
        {
            if (Plugin.config_LandmineCanSpawnOutside.Value == true && __instance.IsServer && __instance.IsHost)
            {
                if (__instance.currentLevel == null) return;
                // Delete all existing landmines
                Utils.OutsideLandmineMarker[] outsideLandmines = GameObject.FindObjectsOfType<Utils.OutsideLandmineMarker>();
                foreach (Utils.OutsideLandmineMarker landmineMarker in outsideLandmines)
                {
                    GameObject.Destroy(landmineMarker.gameObject);
                }

                Plugin.log.LogInfo("Load landmine");
                SelectableLevel selectableLevel = __instance.currentLevel;
                SpawnableMapObject[] spawnableMapObjects = selectableLevel.spawnableMapObjects;
                if (spawnableMapObjects != null && spawnableMapObjects.Length != 0)
                {
                    Plugin.log.LogInfo("Spawn landmine outside");
                    foreach (SpawnableMapObject spawnObject in spawnableMapObjects)
                    {
                        if (spawnObject.prefabToSpawn.GetComponentInChildren<Landmine>() != null)
                        {
                            AnimationCurve landminecurve = new AnimationCurve(new Keyframe[]
                            {
                                new Keyframe(0f, Plugin.config_LandmineMinAmount.Value),
                                new Keyframe(1f, Plugin.config_LandmineMaxAmount.Value)
                            });
                            Transform[] shipSpawnPathPoints = __instance.shipSpawnPathPoints;

                            if (shipSpawnPathPoints == null) return;

                            for (int i = 0; i < shipSpawnPathPoints.Length; i++)
                            {
                                int spawnCount = (int)landminecurve.Evaluate(Random.Range(0f, 1f));
                                for (int j = 0; j < spawnCount; j++)
                                {
                                    System.Random random = new System.Random();

                                    NavMeshHit hit = Traverse.Create(__instance).Field<NavMeshHit>("navHit").Value;

                                    System.Type[] navMethodTypes = new System.Type[] {
                                        typeof(UnityEngine.Vector3),
                                        typeof(float),
                                        typeof(UnityEngine.AI.NavMeshHit),
                                        typeof(System.Random),
                                        typeof(int),
                                        typeof(float)
                                    };

                                    var method = AccessTools.Method(
                                        typeof(RoundManager),
                                        "GetRandomNavMeshPositionInBoxPredictable",
                                        navMethodTypes
                                    );

                                    if (method != null)
                                    {
                                        object[] navParams = new object[] {
                                            shipSpawnPathPoints[i].position,
                                            300f,
                                            hit,
                                            random,
                                            -1,
                                            1f
                                        };

                                        Vector3 randomPos = (Vector3)method.Invoke(__instance, navParams);

                                        Quaternion rotation;
                                        Vector3 finalPos;
                                        (finalPos, rotation) = Utils.projectToGround(randomPos);

                                        GameObject gameObject = Object.Instantiate(spawnObject.prefabToSpawn, finalPos, rotation);
                                        gameObject.SetActive(true);
                                        gameObject.GetComponent<NetworkObject>().Spawn();
                                        gameObject.AddComponent<Utils.OutsideLandmineMarker>();
                                    } else
                                    {
                                        Plugin.log.LogError("FAILED TO FIND METHOD: Even with reflection and parameter counting.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}


