# 구슬 이미지 (#229, #294)

사용자 제공 PNG를 바이트 변경 없이 보존한다. Yin=음, Yang=양, Fire=화, Water=수, Wood=목, Metal=금, Earth=토다.

`C6 Lab/Apply Yin Yang And Element Orb Sprites`는 중앙 피벗·무압축·mipmap 없음의 Sprite로 가져오고 기존 LabOrbAppearance.asset에 연결한다. Config와 Scene 참조는 유지한다.

조합판은 기존 구슬 지름에 맞춰 표시하고, 투척 중에는 각 화면의 카메라를 향한다. PNG에 포함된 색·질감·하이라이트를 사용한다. Collider/Rigidbody·조합·피해·색칠 RGB·축소 비율·궤적은 기존 경로다. 이미지가 없는 종류와 아이템은 기존 3D 표현을 사용한다. 각 Sprite 슬롯을 비우면 해당 종류는 기존 머터리얼/프리팹 경로로 돌아간다.

음·양의 표시 지름은 원래 크기를 유지하고, 오행 이미지 지름은 원래 크기의120%로 표시한다. 조합판과 투척 중 같은 확대 비율을 사용한다. 원본 PNG와 기존 충돌/잡기/조합 반지름은 유지한다.

#294에서 사용자 제공 새 음·양·화·수·목·금·토 PNG로 교체했다. 원본 바이트를 그대로 사용하며 음·양은90×86, 오행은92×92다. 기존 파일명과 `.meta` GUID, Appearance·Config·Scene 연결을 유지한다.
