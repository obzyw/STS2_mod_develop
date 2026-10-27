"""Analyze sprite alpha into atlas regions; does not modify the generated image."""
from collections import deque
from pathlib import Path
import json
import numpy as np
from PIL import Image

path = Path(__file__).with_name("stone-fragments-v2.png")
mask = np.asarray(Image.open(path).convert("RGBA"))[:, :, 3] > 96
height, width = mask.shape
regions = []
for y in range(height):
    for x in np.flatnonzero(mask[y]):
        if not mask[y, x]:
            continue
        pending = deque([(int(x), y)])
        mask[y, x] = False
        left = right = int(x)
        top = bottom = y
        area = 0
        while pending:
            px, py = pending.popleft()
            area += 1
            left, right = min(left, px), max(right, px)
            top, bottom = min(top, py), max(bottom, py)
            for nx, ny in ((px-1, py), (px+1, py), (px, py-1), (px, py+1)):
                if 0 <= nx < width and 0 <= ny < height and mask[ny, nx]:
                    mask[ny, nx] = False
                    pending.append((nx, ny))
        if area > 100:
            regions.append((area, [left, top, right-left+1, bottom-top+1]))
regions = [r for _, r in sorted(regions, reverse=True)[:16]]
regions.sort(key=lambda r: (round((r[1]+r[3]/2)/300), r[0]))
assert len(regions) == 16, f"Expected 16 fragments, found {len(regions)}"
path.with_suffix(".regions.txt").write_text(json.dumps(regions, indent=2))
print(f"Atlas: {width} x {height}; {len(regions)} alpha-bounded regions")
