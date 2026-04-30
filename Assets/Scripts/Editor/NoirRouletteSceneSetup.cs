using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace NoirRoulette
{
    // ─────────────────────────────────────────────────────────────
    // 컴파일 완료 시 자동 실행 — 사용자 조작 불필요
    // ─────────────────────────────────────────────────────────────
    [InitializeOnLoad]
    public static class NoirRouletteAutoSetup
    {
        static NoirRouletteAutoSetup()
        {
            // Unity가 완전히 초기화된 후 실행하도록 딜레이 적용
            EditorApplication.delayCall += TryAutoSetup;
        }

        private static void TryAutoSetup()
        {
            // GameManager가 이미 씬에 있으면 설정 완료 상태 → 스킵
            if (GameObject.Find("GameManager") != null) return;

            Debug.Log("[NOIR ROULETTE] GameManager 없음 → 씬 자동 설정 시작...");
            NoirRouletteSceneSetup.SetupScene();
        }
    }

    // ─────────────────────────────────────────────────────────────
    // NOIR ROULETTE 씬 자동 설정 에디터 스크립트
    // 자동 실행 외에 수동으로도 가능:
    // Unity 상단 메뉴 → NoirRoulette → 씬 자동 설정 ▶ 클릭
    // ─────────────────────────────────────────────────────────────
    public static class NoirRouletteSceneSetup
    {
        private static Font _font;

        [MenuItem("NoirRoulette/씬 자동 설정 ▶")]
        public static void SetupScene()
        {
            // 기본 폰트 로드 (Unity 버전별 경로 다를 수 있어 순차 시도)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 14);

            Debug.Log("=== [NOIR ROULETTE] 씬 자동 설정 시작 ===");

            // 기존 오브젝트 정리
            CleanupExisting();

            // ── 1. 카드 ScriptableObject 에셋 생성 ──
            CardData[] cards = CreateCardAssets();

            // ── 2. 시스템 Empty 오브젝트 생성 + 스크립트 추가 ──
            var gm    = MakeGO<GameManager>("GameManager");
            var cyl   = MakeGO<CylinderSystem>("CylinderSystem");
            var pDeck = MakeGO<DeckManager>("PlayerDeckManager");
            var vDeck = MakeGO<DeckManager>("VillainDeckManager");
            var pCtrl = MakeGO<PlayerController>("PlayerController");
            var vAI   = MakeGO<VillainAI>("VillainAI");

            // 카드 에셋 연결 (플레이어/빌런 덱 공통)
            pDeck.allCardAssets = cards;
            vDeck.allCardAssets = cards;

            // ── 3. Canvas 생성 ──
            var canvas = SetupCanvas();

            // UIManager 빈 오브젝트 (Canvas 하위)
            var uiMgrGO = new GameObject("UIManager_GO");
            uiMgrGO.transform.SetParent(canvas.transform, false);
            var uiMgr = uiMgrGO.AddComponent<UIManager>();

            // ── 4. UI 텍스트 생성 (앵커 기반 배치) ──

            // 턴 표시 (상단 중앙)
            var turnTxt = MakeTxt("TurnText", canvas.transform, "플레이어 턴",
                18, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.93f), new Vector2(0.75f, 0.99f));

            // 빌런 정보 (상단)
            var vHPTxt     = MakeTxt("VillainHP_Text", canvas.transform, "빌런 HP: 3",
                15, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0.86f), new Vector2(0.30f, 0.92f));
            var vMentalTxt = MakeTxt("VillainMental_Text", canvas.transform, "멘탈: 3",
                15, TextAnchor.MiddleLeft,
                new Vector2(0.30f, 0.86f), new Vector2(0.52f, 0.92f));
            var vStateTxt  = MakeTxt("VillainState_Text", canvas.transform, "상태: CALM",
                15, TextAnchor.MiddleLeft,
                new Vector2(0.52f, 0.86f), new Vector2(0.76f, 0.92f));
            var vHandTxt   = MakeTxt("VillainHand_Text", canvas.transform, "빌런 핸드: 0장",
                13, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0.81f), new Vector2(0.50f, 0.86f));

            // 실린더 표시 (중앙)
            var cylTxt = MakeTxt("Cylinder_Text", canvas.transform, "실린더: ...",
                15, TextAnchor.MiddleCenter,
                new Vector2(0.02f, 0.73f), new Vector2(0.78f, 0.80f));

            // 플레이어 HP (하단)
            var pHPTxt = MakeTxt("PlayerHP_Text", canvas.transform, "플레이어 HP: 2 (조직원1+본인)",
                15, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0.06f), new Vector2(0.60f, 0.12f));

            // ── 5. 핸드 패널 (카드 버튼 동적 생성 부모) ──
            var handPanelGO = new GameObject("HandPanel");
            handPanelGO.transform.SetParent(canvas.transform, false);
            {
                var img = handPanelGO.AddComponent<Image>();
                img.color = new Color(0.12f, 0.12f, 0.18f, 0.7f);
                var rt = handPanelGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.02f, 0.13f);
                rt.anchorMax = new Vector2(0.78f, 0.26f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;

                var hlg = handPanelGO.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 8;
                hlg.padding = new RectOffset(8, 8, 8, 8);
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = true;
                hlg.childControlWidth      = false;
                hlg.childControlHeight     = true;
                hlg.childAlignment         = TextAnchor.MiddleLeft;
            }

            // ── 6. 발사 / 턴 종료 버튼 ──
            var shootBtnGO   = MakeBtn("ShootButton",   canvas.transform, "발  사",
                new Vector2(0.02f, 0.01f), new Vector2(0.20f, 0.07f));
            var endTurnBtnGO = MakeBtn("EndTurnButton", canvas.transform, "턴 종료",
                new Vector2(0.22f, 0.01f), new Vector2(0.40f, 0.07f));

            // ── 7. 로그 패널 (ScrollView) ──
            var logScrollGO = new GameObject("LogPanel");
            logScrollGO.transform.SetParent(canvas.transform, false);
            {
                var img = logScrollGO.AddComponent<Image>();
                img.color = new Color(0.05f, 0.05f, 0.10f, 0.85f);
                var rt = logScrollGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.02f, 0.27f);
                rt.anchorMax = new Vector2(0.78f, 0.72f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            var scrollRect = logScrollGO.AddComponent<ScrollRect>();

            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(logScrollGO.transform, false);
            {
                var img = viewportGO.AddComponent<Image>();
                img.color = new Color(1, 1, 1, 0);
                viewportGO.AddComponent<Mask>().showMaskGraphic = false;
                FullStretch(viewportGO.GetComponent<RectTransform>());
            }

            // Content
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT;
            {
                contentRT = contentGO.AddComponent<RectTransform>();
                contentRT.anchorMin = new Vector2(0, 1);
                contentRT.anchorMax = new Vector2(1, 1);
                contentRT.pivot     = new Vector2(0.5f, 1f);
                contentRT.sizeDelta = new Vector2(0, 200);
                contentRT.anchoredPosition = Vector2.zero;
                var csf = contentGO.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // LogText (UIManager가 직접 참조)
            var logTextGO = new GameObject("LogText");
            logTextGO.transform.SetParent(contentGO.transform, false);
            var logTxt = logTextGO.AddComponent<Text>();
            logTxt.font      = _font;
            logTxt.fontSize  = 30;
            logTxt.color     = new Color(0.9f, 0.9f, 0.9f, 1f);
            logTxt.alignment = TextAnchor.UpperLeft;
            logTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            logTxt.verticalOverflow   = VerticalWrapMode.Overflow;
            {
                var rt = logTextGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(5, 5);
                rt.offsetMax = new Vector2(-5, -5);
            }

            // ScrollRect 연결
            scrollRect.content          = contentRT;
            scrollRect.viewport         = viewportGO.GetComponent<RectTransform>();
            scrollRect.horizontal       = false;
            scrollRect.vertical         = true;
            scrollRect.scrollSensitivity = 30;
            scrollRect.movementType     = ScrollRect.MovementType.Clamped;

            // ── 8. 게임오버 패널 (기본 비활성) ──
            var gameOverGO = new GameObject("GameOverPanel");
            gameOverGO.transform.SetParent(canvas.transform, false);
            {
                var img = gameOverGO.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.88f);
                FullStretch(gameOverGO.GetComponent<RectTransform>());
            }
            var resultTitleTxt = MakeTxt("ResultTitle_Text", gameOverGO.transform, "결과",
                40, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.78f));
            var resultScriptTxt = MakeTxt("ResultScript_Text", gameOverGO.transform, "",
                20, TextAnchor.MiddleCenter,
                new Vector2(0.10f, 0.35f), new Vector2(0.90f, 0.62f));
            resultScriptTxt.verticalOverflow = VerticalWrapMode.Overflow;
            var restartBtnGO = MakeBtn("RestartButton", gameOverGO.transform, "다시 시작",
                new Vector2(0.30f, 0.10f), new Vector2(0.70f, 0.25f));
            gameOverGO.SetActive(false); // 기본 비활성

            // ── 9. 디버그 패널 (우측) ──
            var debugPanelGO = new GameObject("DebugPanel");
            debugPanelGO.transform.SetParent(canvas.transform, false);
            {
                var img = debugPanelGO.AddComponent<Image>();
                img.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
                var rt = debugPanelGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.80f, 0f);
                rt.anchorMax = new Vector2(1.00f, 1f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            var dpComp = debugPanelGO.AddComponent<DebugPanel>();

            // 디버그 텍스트들
            var dbgCylTxt   = MakeTxt("Debug_CylinderText",    debugPanelGO.transform, "[실린더]",
                10, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.80f), new Vector2(0.98f, 0.98f));
            var dbgPDeckTxt = MakeTxt("Debug_PlayerDeckText",  debugPanelGO.transform, "[플레이어 덱]",
                10, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.60f), new Vector2(0.98f, 0.80f));
            var dbgVDeckTxt = MakeTxt("Debug_VillainDeckText", debugPanelGO.transform, "[빌런 덱]",
                10, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.42f), new Vector2(0.98f, 0.60f));
            var dbgBluffTxt = MakeTxt("Debug_BluffText",       debugPanelGO.transform, "[블러핑]",
                10, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.35f), new Vector2(0.98f, 0.42f));
            var dbgTurnTxt  = MakeTxt("Debug_TurnText",        debugPanelGO.transform, "[상태]",
                10, TextAnchor.UpperLeft,
                new Vector2(0.02f, 0.24f), new Vector2(0.98f, 0.35f));

            // 디버그 버튼들
            var dbgReloadBtnGO   = MakeBtn("ReloadCylinderButton", debugPanelGO.transform, "실린더 재장전",
                new Vector2(0.02f, 0.13f), new Vector2(0.98f, 0.23f));
            var dbgShowDeckBtnGO = MakeBtn("ShowDeckButton",        debugPanelGO.transform, "덱 확인 (Console)",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.12f));

            // ── 10. CardButtonPrefab 생성 ──
            var cardPrefab = CreateCardButtonPrefab();

            // ── 11. 모든 참조 연결 ──

            // UIManager 연결
            uiMgr.villainHP_Text     = vHPTxt;
            uiMgr.villainMental_Text = vMentalTxt;
            uiMgr.villainState_Text  = vStateTxt;
            uiMgr.villainHand_Text   = vHandTxt;
            uiMgr.cylinder_Text      = cylTxt;
            uiMgr.playerHP_Text      = pHPTxt;
            uiMgr.handPanel          = handPanelGO.transform;
            uiMgr.shootButton        = shootBtnGO.GetComponent<Button>();
            uiMgr.endTurnButton      = endTurnBtnGO.GetComponent<Button>();
            uiMgr.turnText           = turnTxt;
            uiMgr.logText            = logTxt;
            uiMgr.gameOverPanel      = gameOverGO;
            uiMgr.resultTitle_Text   = resultTitleTxt;
            uiMgr.resultScript_Text  = resultScriptTxt;
            uiMgr.restartButton      = restartBtnGO.GetComponent<Button>();
            uiMgr.playerController   = pCtrl;
            uiMgr.cardButtonPrefab   = cardPrefab;

            // DebugPanel 연결
            dpComp.cylinderSystem     = cyl;
            dpComp.playerDeckManager  = pDeck;
            dpComp.villainDeckManager = vDeck;
            dpComp.villainAI          = vAI;
            dpComp.gameManager        = gm;
            dpComp.debug_CylinderText    = dbgCylTxt;
            dpComp.debug_PlayerDeckText  = dbgPDeckTxt;
            dpComp.debug_VillainDeckText = dbgVDeckTxt;
            dpComp.debug_BluffText       = dbgBluffTxt;
            dpComp.debug_TurnText        = dbgTurnTxt;
            dpComp.reloadCylinderButton  = dbgReloadBtnGO.GetComponent<Button>();
            dpComp.showDeckButton        = dbgShowDeckBtnGO.GetComponent<Button>();

            // GameManager 연결
            gm.cylinderSystem     = cyl;
            gm.playerDeckManager  = pDeck;
            gm.villainDeckManager = vDeck;
            gm.playerController   = pCtrl;
            gm.villainAI          = vAI;
            gm.uiManager          = uiMgr;

            // 씬 저장 (참조가 도메인 리로드 후에도 유지되도록 반드시 저장)
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log("=== [NOIR ROULETTE] 씬 설정 완료! ▶ 버튼으로 테스트하세요. ===");
        }

        // ─────────────────────────────────────────
        // 기존 오브젝트 정리
        // ─────────────────────────────────────────
        private static void CleanupExisting()
        {
            string[] names = {
                "GameManager", "CylinderSystem", "PlayerDeckManager",
                "VillainDeckManager", "PlayerController", "VillainAI",
                "Canvas", "EventSystem", "UIManager_GO"
            };
            foreach (var n in names)
            {
                var obj = GameObject.Find(n);
                if (obj != null) Object.DestroyImmediate(obj);
            }
        }

        // ─────────────────────────────────────────
        // Empty GO 생성 + 스크립트 추가
        // ─────────────────────────────────────────
        private static T MakeGO<T>(string name) where T : Component
        {
            return new GameObject(name).AddComponent<T>();
        }

        // ─────────────────────────────────────────
        // Canvas + EventSystem 생성
        // ─────────────────────────────────────────
        private static Canvas SetupCanvas()
        {
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem (없을 때만 생성)
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();

                // InputSystem 패키지 설치 여부 확인 (리플렉션)
                var inputSysType = System.Type.GetType(
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputSysType != null)
                    esGO.AddComponent(inputSysType);
                else
                    esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        // ─────────────────────────────────────────
        // 레거시 Text 생성 (앵커 기반 배치)
        // ─────────────────────────────────────────
        private static Text MakeTxt(string name, Transform parent, string content,
                                    int fontSize, TextAnchor anchor,
                                    Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text      = content;
            txt.font      = _font;
            txt.fontSize  = 30;  // 모든 텍스트 30pt 통일
            txt.color     = Color.white;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return txt;
        }

        // ─────────────────────────────────────────
        // 버튼 생성
        // ─────────────────────────────────────────
        private static GameObject MakeBtn(string name, Transform parent, string label,
                                          Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.38f, 1f);
            go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 버튼 텍스트
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.text      = label;
            txt.font      = _font;
            txt.fontSize  = 30;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            return go;
        }

        // RectTransform 전체 스트레치 (부모 100%)
        private static void FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ─────────────────────────────────────────
        // CardData ScriptableObject 에셋 생성 (13종)
        // ─────────────────────────────────────────
        private static CardData[] CreateCardAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Cards"))
                AssetDatabase.CreateFolder("Assets", "Cards");

            var defs = new (string nm, CardType ct, int qty, string desc)[]
            {
                ("조준",       CardType.조준,       3, "이번 발사를 상대에게. 명중 시 HP -1."),
                ("급소",       CardType.급소,       1, "상대에게. 명중 HP -2 / 공탄 내 HP -1."),
                ("실린더섞기", CardType.실린더섞기, 2, "실린더 랜덤 리셋. 탄 정보 무효."),
                ("총알추가",   CardType.총알추가,   2, "실탄 1발 추가."),
                ("카드뺏기",   CardType.카드뺏기,   2, "상대 핸드 랜덤 1장 버림."),
                ("허공쏘기",   CardType.허공쏘기,   2, "현재 탄 소모, 실탄/공탄 확인."),
                ("탄확인",     CardType.탄확인,     2, "임의 칸 탄 확인 (소모 없음)."),
                ("협박",       CardType.협박,       2, "빌런 멘탈 -1."),
                ("조롱",       CardType.조롱,       1, "직전 빌런 실수 시만. 멘탈 -2."),
                ("눈치채기",   CardType.눈치채기,   2, "블러핑 간파. 성공 멘탈-2 / 실패 내 HP-1."),
                ("체력회복",   CardType.체력회복,   2, "HP +1 (최대 3)."),
                ("잼",         CardType.잼,         1, "다음 발사 불발. 탄 소모 없음."),
                ("실린더넘기기", CardType.실린더넘기기, 1, "현재 탄 → 6번째 칸.")
            };

            var result = new CardData[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                string path = $"Assets/Cards/Card_{defs[i].nm}.asset";

                // 기존 에셋 재사용
                var existing = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (existing != null) { result[i] = existing; continue; }

                var card = ScriptableObject.CreateInstance<CardData>();
                card.cardName    = defs[i].nm;
                card.cardType    = defs[i].ct;
                card.quantity    = defs[i].qty;
                card.description = defs[i].desc;
                AssetDatabase.CreateAsset(card, path);
                result[i] = card;
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        // ─────────────────────────────────────────
        // CardButtonPrefab 생성 (Assets/Resources/)
        // ─────────────────────────────────────────
        private static GameObject CreateCardButtonPrefab()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            string path = "Assets/Resources/CardButtonPrefab.prefab";

            // 기존 프리팹 재사용
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("CardButtonPrefab");
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.22f, 0.38f, 1f);
            go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130, 70);

            // 버튼 텍스트
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.font      = _font;
            txt.fontSize  = 30;
            txt.color     = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow   = VerticalWrapMode.Overflow;
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4, 4);
            trt.offsetMax = new Vector2(-4, -4);

            // 프리팹으로 저장
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
