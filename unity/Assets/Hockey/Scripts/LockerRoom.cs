// Раздевалка: отдельная комната под ареной — деревянные шкафчики, скамья, свитера на вешалках, клюшки.
// Сюда переносит кнопка «Раздевалка»: игрок сидит на скамье у своего шкафчика.
using UnityEngine;
using UnityEngine.Rendering;

namespace Hockey
{
    public class LockerRoom
    {
        public readonly Transform Root;
        public Vector3 Seat => Root.position + new Vector3(0.9f, 0, 1.59f);
        public Vector3 CamPos => Root.position + new Vector3(0.35f, 1.25f, -1.75f);
        public Vector3 CamLook => Root.position + new Vector3(-0.35f, 0.8f, 1.8f);
        MeshRenderer plate;
        /// Слой комнаты: прожекторы арены её не освещают (иначе пересвет), лампа комнаты светит только на неё.
        public const int Layer = 10;

        static GameObject Obj(Transform parent, Mesh mesh, Material mat, Vector3 pos, bool cast = true)
        {
            var go = new GameObject("p");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.layer = Layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        public LockerRoom(Transform parent, Vector3 origin, int number)
        {
            Root = new GameObject("LockerRoom").transform;
            Root.SetParent(parent, false);
            Root.position = origin;
            var tc = TeamColors.All[0];
            Material wood = Mats.Get("#c08a55", 0.2f), woodDark = Mats.Get("#8f5f36", 0.2f), wall = Mats.Get("#ece6d8", 0.05f),
                floor = Mats.Get("#30344a", 0.3f), mat = Mats.Get("#3d4260", 0.1f), team = Mats.Get(tc.Main, 0.12f), teamDark = Mats.Get(tc.Dark, 0.12f),
                white = Mats.Get("#f6f6f2", 0.1f), black = Mats.Get("#26262e", 0.3f), steel = Mats.Get("#c9d2de", 0.5f);
            const float W = 8, D = 5.2f, H = 3;
            // коробка комнаты (стены и потолок не отбрасывают тень — иначе внутри темно)
            Obj(Root, MeshKit.Box(W, 0.1f, D), floor, new Vector3(0, -0.05f, 0), false);
            Obj(Root, MeshKit.Box(W, H, 0.1f), wall, new Vector3(0, H / 2, D / 2), false);
            Obj(Root, MeshKit.Box(W, H, 0.1f), wall, new Vector3(0, H / 2, -D / 2), false);
            Obj(Root, MeshKit.Box(0.1f, H, D), wall, new Vector3(-W / 2, H / 2, 0), false);
            Obj(Root, MeshKit.Box(0.1f, H, D), wall, new Vector3(W / 2, H / 2, 0), false);
            Obj(Root, MeshKit.Box(W, 0.1f, D), white, new Vector3(0, H, 0), false);
            // резиновые дорожки и полоса цвета команды по стене
            Obj(Root, MeshKit.Box(W - 0.4f, 0.012f, 1.5f), mat, new Vector3(0, 0.006f, 0.9f), false);
            Obj(Root, MeshKit.Box(W, 0.22f, 0.02f), team, new Vector3(0, 2.45f, D / 2 - 0.06f), false);
            Obj(Root, MeshKit.Box(W, 0.06f, 0.02f), white, new Vector3(0, 2.28f, D / 2 - 0.06f), false);
            // светильники
            for (int i = -1; i <= 1; i++) Obj(Root, MeshKit.Box(1.6f, 0.05f, 0.35f), Mats.Emissive("#fff6e0", 0.7f), new Vector3(i * 2.6f, H - 0.08f, 0.4f), false);
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(Root, false);
            lamp.transform.localPosition = new Vector3(0.4f, 2.4f, 0.2f);
            lamp.cullingMask = 1 << Layer;
            lamp.type = LightType.Point; lamp.range = 10; lamp.intensity = 0.75f; lamp.color = Mats.Hex("#ffe9c8"); lamp.shadows = LightShadows.None;

            // шкафчики вдоль задней стены: перегородки, полка, скамья
            float back = D / 2 - 0.05f, stallW = 1.3f; int n = 6;
            float x0 = -stallW * n / 2;
            for (int i = 0; i <= n; i++) Obj(Root, MeshKit.Box(0.07f, 2.05f, 0.62f), wood, new Vector3(x0 + i * stallW, 1.025f, back - 0.31f));
            Obj(Root, MeshKit.Box(stallW * n + 0.07f, 0.07f, 0.64f), wood, new Vector3(0, 1.95f, back - 0.32f));
            Obj(Root, MeshKit.Box(stallW * n + 0.07f, 0.4f, 0.03f), woodDark, new Vector3(0, 2.2f, back - 0.02f), false);
            Obj(Root, MeshKit.Box(stallW * n + 0.07f, 0.08f, 0.56f), wood, new Vector3(0, 0.3f, back - 0.36f));        // скамья (верх на 0.34)
            Obj(Root, MeshKit.Box(stallW * n + 0.07f, 0.26f, 0.05f), woodDark, new Vector3(0, 0.13f, back - 0.6f));
            Obj(Root, MeshKit.Box(stallW * n, 0.03f, 0.03f), steel, new Vector3(0, 1.72f, back - 0.3f));               // штанга для вешалок
            int[] nums = { 9, 21, 0, 88, 13, 4 };
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + (i + 0.5f) * stallW;
                bool mine = i == 3;   // шкафчик игрока: свитер снят (он на игроке), сверху табличка с номером
                // шлем на полке
                var helm = Obj(Root, MeshKit.Sphere(0.17f, 14, 0), mine ? team : (i % 2 == 0 ? team : white), new Vector3(cx - 0.3f, 2.12f, back - 0.3f));
                helm.transform.localScale = new Vector3(1, 0.85f, 1.15f);
                if (!mine)
                {
                    // свитер на вешалке: тело, рукава, белая полоса, номер
                    var j = new GameObject("jersey").transform; j.SetParent(Root, false); j.localPosition = new Vector3(cx, 1.3f, back - 0.3f);
                    Obj(j, MeshKit.Box(0.56f, 0.66f, 0.07f), team, Vector3.zero);
                    foreach (int sd in new[] { -1, 1 })
                    {
                        var sl = Obj(j, MeshKit.Box(0.2f, 0.5f, 0.07f), team, new Vector3(sd * 0.36f, 0.06f, 0));
                        sl.transform.localRotation = Quaternion.Euler(0, 0, sd * 22);
                        Obj(sl.transform, MeshKit.Box(0.215f, 0.07f, 0.095f), white, new Vector3(0, -0.14f, 0), false);
                    }
                    Obj(j, MeshKit.Box(0.575f, 0.09f, 0.095f), white, new Vector3(0, -0.22f, 0), false);
                    Obj(j, MeshKit.Box(0.3f, 0.03f, 0.03f), steel, new Vector3(0, 0.36f, 0), false);
                    Obj(j, MeshKit.Plane(0.34f, 0.34f), Mats.Cutout(Tex.Number(nums[i] == 0 ? 7 : nums[i]), Color.white), new Vector3(0, 0.02f, -0.06f), false);
                }
                // коньки под скамьёй и сумка
                Obj(Root, MeshKit.Box(0.34f, 0.2f, 0.3f), i % 3 == 0 ? teamDark : black, new Vector3(cx + 0.35f, 0.1f, back - 0.2f));
                // табличка
                var pl = Obj(Root, MeshKit.Box(0.42f, 0.24f, 0.02f), black, new Vector3(cx, 2.2f, back - 0.05f), false);
                if (mine)
                {
                    pl.transform.localScale = new Vector3(1.25f, 1.25f, 1);
                    plate = Obj(Root, MeshKit.Plane(0.27f, 0.27f), Mats.Cutout(Tex.Number(number), Color.white), new Vector3(cx, 2.2f, back - 0.09f), false).GetComponent<MeshRenderer>();
                }
            }
            // клюшки у боковой стены
            for (int i = 0; i < 4; i++)
            {
                var st = Obj(Root, MeshKit.Box(0.035f, 1.45f, 0.025f), black, new Vector3(-W / 2 + 0.35f, 0.74f, 1.2f - i * 0.28f));
                st.transform.localRotation = Quaternion.Euler(-4, 0, -10);
                Obj(st.transform, MeshKit.Box(0.035f, 0.09f, 0.3f), i % 2 == 0 ? white : team, new Vector3(0, -0.7f, -0.14f));
            }
            // эмблема на полу
            var logo = Obj(Root, MeshKit.Cylinder(0.9f, 0.012f, 40), team, new Vector3(-1.2f, 0.012f, -0.3f), false);
            Obj(Root, MeshKit.Cylinder(0.62f, 0.014f, 40), white, new Vector3(-1.2f, 0.014f, -0.3f), false);
            Obj(Root, MeshKit.Cylinder(0.3f, 0.05f, 28), black, new Vector3(-1.2f, 0.03f, -0.3f));   // шайба
        }

        public void SetNumber(int number)
        {
            if (plate != null) plate.sharedMaterial = Mats.Cutout(Tex.Number(number), Color.white);
        }
    }
}
