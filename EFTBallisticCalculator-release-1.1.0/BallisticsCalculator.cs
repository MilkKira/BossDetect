using BepInEx.Configuration;
using EFT.Ballistics; // 引入 G1 阻力计算
using EFTBallisticCalculator.Locale;
using System;
using UnityEngine;

namespace EFTBallisticCalculator
{
    /// <summary>
    /// 核心弹道解算器类，使用近似塔科夫 G1 空气动力学模型的算法模拟弹道轨迹
    /// </summary>
    public static class BallisticsCalculator
    {
        private static readonly Vector3 Gravity = Physics.gravity;
        public static ConfigEntry<float> Scale;
        public static void InitCfg(ConfigFile config)
        {
            Scale = config.Bind("Global / 全局设置", "着弹点标记缩放", 0.015f,
                new ConfigDescription(CfgLocaleManager.Get("cfg_calc_scale_desc"), 
                new AcceptableValueRange<float>(0f, 3f),
                new ConfigurationManagerAttributes { DispName = CfgLocaleManager.Get("cfg_calc_scale_name"), IsAdvanced = true }));
        }
        public static Vector3 SimulateToHorizontalDistance(
         Vector3 startPos, Vector3 vel, float mass, float diam, float bc, float targetH, out float timeOfFlight)
        {
            float mKg = mass / 1000f;
            float dM = diam / 1000f;
            float dragMult = (mKg * 0.0014223f) / (dM * dM * bc);
            float airFactor = 1.2f * (dM * dM * Mathf.PI / 4f);

            Vector3 pos = startPos;
            float dt = 0.01f;
            timeOfFlight = 0f;

            for (int tick = 0; tick < 1000; tick++)
            {
                float vMag = vel.magnitude;
                float dragAcc = EftBulletClass.CalculateG1DragCoefficient(vMag) * dragMult;
                Vector3 acc = Gravity + (airFactor * -dragAcc * vMag * vMag / (mKg * 2f)) * vel.normalized;

                Vector3 nextPos = pos + vel * dt + 0.5f * acc * dt * dt;
                Vector3 nextVel = vel + acc * dt;

                float currH = new Vector2(pos.x - startPos.x, pos.z - startPos.z).magnitude;
                float nextH = new Vector2(nextPos.x - startPos.x, nextPos.z - startPos.z).magnitude;

                if (nextH >= targetH)
                {
                    float t = (targetH - currH) / (nextH - currH);
                    timeOfFlight += dt * t;
                    return Vector3.Lerp(pos, nextPos, t);
                }

                pos = nextPos;
                vel = nextVel;
                timeOfFlight += dt;
            }
            return pos;
        }
        public static void UpdateImpactMarker()
        {
            bool hasLockedDistance = PluginsCore._lockedHorizontalDist > 0f;
            bool isFcsLocked = (PluginsCore._currentFC != null) && PluginsCore._hasAmmo && hasLockedDistance;
            if (isFcsLocked)
            {
                bool isAiming = false;
                var pwa = PluginsCore.CorrectPlayer.ProceduralWeaponAnimation;
                if (pwa != null) isAiming = pwa.IsAiming;

                if (isAiming)
                {
                    if (PluginsCore._impactMarker == null) InitializeMarker();

                    Vector3 currentFireportPos = PluginsCore._currentFC.CurrentFireport.position;
                    Vector3 currentBoreDir = PluginsCore._currentFC.WeaponDirection.normalized;
                    Vector3 currentVelocity = currentBoreDir * PluginsCore._currentSpeed;

                    Vector3 impactPoint3D = BallisticsCalculator.SimulateToHorizontalDistance(
                        currentFireportPos, currentVelocity, PluginsCore._currentMass, PluginsCore._currentDiam, PluginsCore._currentBC, PluginsCore._lockedHorizontalDist, out PluginsCore._lockedTOF
                    );

                    PluginsCore._impactMarker.transform.position = impactPoint3D;

                    Camera activeCam = (PluginsCore._cachedOpticCamera != null && PluginsCore._cachedOpticCamera.isActiveAndEnabled) ? PluginsCore._cachedOpticCamera : Camera.main;
                    float distToCamera = Vector3.Distance(activeCam.transform.position, impactPoint3D);
                    float dynamicScale = distToCamera * Mathf.Tan(activeCam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Scale.Value;
                    PluginsCore._impactMarker.transform.localScale = new Vector3(dynamicScale, dynamicScale, dynamicScale);

                    if (!PluginsCore._impactMarker.activeSelf) PluginsCore._impactMarker.SetActive(true);
                }
                else
                {
                    if (PluginsCore._impactMarker != null && PluginsCore._impactMarker.activeSelf) PluginsCore._impactMarker.SetActive(false);
                }
            }
            else
            {
                if (PluginsCore._impactMarker != null && PluginsCore._impactMarker.activeSelf) PluginsCore._impactMarker.SetActive(false);
            }
        }
        public static void InitializeMarker()
        {
            PluginsCore._impactMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(PluginsCore._impactMarker.GetComponent<Collider>());
            Material mat = new Material(Shader.Find("GUI/Text Shader"));
            mat.color = Color.yellow;
            MeshRenderer renderer = PluginsCore._impactMarker.GetComponent<MeshRenderer>();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        public static void ExecuteFcsLogic()
        {
            if (PluginsCore._currentFC == null) return;
            Camera activeCam = (PluginsCore._cachedOpticCamera != null && PluginsCore._cachedOpticCamera.isActiveAndEnabled)
                ? PluginsCore._cachedOpticCamera
                : Camera.main;

            if (activeCam == null) return; // 防御性空值检查
            Ray ray = activeCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 1000f, PluginsCore._layerMask))
            {
                Vector3 fireportPos = PluginsCore._currentFC.CurrentFireport.position;
                PluginsCore._lockedHorizontalDist = new Vector2(hit.point.x - fireportPos.x, hit.point.z - fireportPos.z).magnitude;
            }
            else
            {
                PluginsCore._lockedHorizontalDist = 0f;
            }
        }
        public static void UpdateOpticCache()
        {
            var pwa = PluginsCore.CorrectPlayer.ProceduralWeaponAnimation;
            if (pwa != null && pwa.IsAiming)
            {
                if (PluginsCore._cachedOpticCamera == null || !PluginsCore._cachedOpticCamera.isActiveAndEnabled)
                {
                    foreach (Camera cam in Camera.allCameras)
                    {
                        if (cam.isActiveAndEnabled && cam != Camera.main && cam.targetTexture != null)
                        {
                            PluginsCore._cachedOpticCamera = cam;
                            break;
                        }
                    }
                }
            }
            else { PluginsCore._cachedOpticCamera = null; }
        }
        public static void UpdateFCSData()
        {
            PluginsCore._currentFC = PluginsCore.CorrectPlayer.HandsController as EFT.Player.FirearmController;

            if (PluginsCore._currentFC != null && PluginsCore._currentFC.Item != null && PluginsCore._currentFC.Item.Chambers.Length > 0 && PluginsCore._currentFC.Item.Chambers[0].ContainedItem is AmmoItemClass currentAmmo)
            {
                PluginsCore._hasAmmo = true;
                PluginsCore._currentSpeed = currentAmmo.AmmoTemplate.InitialSpeed * PluginsCore._currentFC.Item.SpeedFactor;
                PluginsCore._currentMass = currentAmmo.AmmoTemplate.BulletMassGram;
                PluginsCore._currentBC = currentAmmo.AmmoTemplate.BallisticCoeficient;
                PluginsCore._currentDiam = currentAmmo.AmmoTemplate.BulletDiameterMilimeters;
            }
            else
            {
                PluginsCore._hasAmmo = false;
            }
        }
        public static Transform GetAzimuth()
        {
            return (PluginsCore._cachedOpticCamera != null && PluginsCore._cachedOpticCamera.isActiveAndEnabled) ? PluginsCore._cachedOpticCamera.transform : Camera.main.transform;
        }
    }
}