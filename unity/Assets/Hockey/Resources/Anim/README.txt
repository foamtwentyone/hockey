Перенос захвата движения (BVH из CMU Motion Capture Database) на персонажа KayKit.
Данные CMU: https://github.com/meshula/cmu-mocap (BVH-версия Bruce Hahne / cgspeed.com).
«The data used in this project was obtained from mocap.cs.cmu.edu. The database was created with funding from NSF EIA-0196217.»

bvh.py      — чтение BVH и прямая кинематика
glb.py, kk.py — чтение Rogue.glb (KayKit) и программный рендер позы в PNG (для проверки без Unity)
retarget.py — направления костей из кадра BVH, отражение под левый хват
sheet.py    — контактный лист «палочных человечков» по кадрам BVH
preview.py  — поза KayKit по кадрам BVH (3 ракурса)
export.py   — экспорт клипов stance / windup / shot в Assets/Hockey/Resources/Anim/*.txt
verify.py   — проверка экспортированных клипов на модели KayKit

Пути к BVH и Rogue.glb в скриптах указаны относительно рабочей папки, где лежат cmu/ и kaykit/.
