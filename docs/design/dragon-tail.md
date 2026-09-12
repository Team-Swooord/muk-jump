# 용꼬리

- 기존 ID 유지, 새 DragonTail = 45. 본체가 용(ChildDragon) 충돌로 실제 사망한 판의 정상 정산 시 해금한다.
- 방어막·체력이 남은 피격·상승 무적·성장 부활·분신 사망·일반 가시·용에게 맞은 뒤 별도로 추락한 사망은 제외한다.
- 사망 알림은 마지막 개체의 게임오버 복구본 작성 전에 판 통계에 포함된다. 기존 계정 성장 저장/뒤끝 동기화 및 해금 연출을 재사용한다. 서버 테이블·SDK 변경은 없다.
- SkinRunStats v1에 선택 bool `diedToDragon`을 추가했다. 예전 복구본에 필드가 없으면 false이며 예전 사망 이력에서 소급 해금하지 않는다. 정상 정산 실패 시에는 기존 원자적 저장 복구와 재시도를 따른다.
- 하찮은 용 그림체의 꼬리를 몸체 뒤에 부착한다. 왼쪽 뿌리를 고정하고 끝만 기존 UI/월드 공통 메시로 흔든다. 동작 줄이기에서는 정지하며 충돌 크기·능력치는 변경하지 않는다.
- 한·영·일 이름, 조건, 한 줄 이야기 포함. 이미지 생성 모드는 내장 image_gen, 실제 투명 alpha PNG.
- 저장 경로: `Assets/Resources/MukJump/Skins/skin_dragon_tail_v1.png`. 스타일 참조: `Assets/Resources/MukJump/Obstacles/child_ink_dragon.png`.

## 최종 생성 프롬프트

Use case: stylized-concept. Asset: single transparent 2D game costume accessory sprite, a little 'Dragon Tail' matching the provided naive child-ink-dragon reference. Reference is STYLE and tail shape only, do not reproduce head/body/legs. Draw ONE chubby, short S-curved tail extending from a broad plain rounded base on the LEFT toward an upturned little three-point arrow/leaf tuft on the RIGHT. Three or four wonky triangular dorsal fins along the upper edge. Warm off-white paper fill, very thick imperfect black crayon/ink outline, two tiny scribble scales at most. Endearingly pathetic and friendly, as if a small child drew it. No realism, no detailed scales, no smooth polished vector edges. It attaches BEHIND the lower-right side of an unchanged round black ink creature: base will be hidden by the creature, tail tip visible to the right. Accessory ONLY, no creature, no eyes, no face, no arms, no legs, no clothes, no text or watermark. Centered orthographic side view occupying 80% of a square canvas, with generous transparent margins. Genuinely transparent background with real alpha; no background rectangle, floor shadow, checkerboard art or gradient.

## 검증

- 용 충돌의 실제 사망·체력 잔여·방어막·상승 무적·일시정지·분신·일반 가시·추락·성장 부활, 저장 실패와 계정 교체, 번역/꼬리 뿌리 고정 검사 11개 통과.
- Play Mode 표시 검사 1개 통과: 용꼬리를 포함한 세 의상의 한·영·일 조건/설명 크기와 무대 경계 검사. 실제 캡처에서 꼬리의 몸통 뒤 부착과 글자 배치를 확인했다.
- 기존 정산 복구 JSON과 해금 저장을 그대로 사용하며 새 서버 데이터 구조를 도입하지 않았다. 메모리 저장소 검사이며 실제 뒤끝/실기기 저장 왕복은 미검증이다.
- 증거: `output/qa/dragon-tail/`의 `rules-tests.json`, `presentation-tests.json`, 한·영·일 PNG. 기존 장애물 임포터의 일관성 경고는 별도로 남아 있어 프로젝트 전체 무경고를 의미하지 않는다.
- 서버 계정 변경·삭제, 네이티브 빌드·TestFlight 업로드는 실행하지 않았다.
