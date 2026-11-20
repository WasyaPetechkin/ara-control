"""Rebuild the Windows icon resources using Pillow."""

from io import BytesIO
from pathlib import Path
import struct

from PIL import Image, ImageDraw


OUT = Path(__file__).resolve().parent
INK = "#172838"
SILVER = "#dae5eb"
RIM = "#b4c6d3"
BLUE = "#244568"
KNOB = "#89afc8"


def draw_icon(size, state):
    # Work on a 64-unit grid. Small tray sizes have their own grille and yoke.
    scale = 8
    grid = 64
    img = Image.new("RGBA", (grid * scale, grid * scale))
    canvas = ImageDraw.Draw(img)

    def box(points):
        return [round(n * scale) for n in points]

    def rounded(points, radius, fill):
        canvas.rounded_rectangle(box(points), radius * scale, fill=fill)

    def oval(points, fill):
        canvas.ellipse(box(points), fill=fill)

    def polygon(points, fill):
        canvas.polygon([tuple(box(p)) for p in points], fill)

    def line(points, fill, width):
        canvas.line([tuple(box(p)) for p in points], fill, width * scale)

    tray = state != "Application"
    small = size <= 24
    # Desktop yoke and flared base establish the microphone silhouette.
    rounded((9, 28, 55, 47), 5, INK)
    rounded((11, 29, 53, 45), 4, RIM)
    rounded((15, 24, 49, 39), 4, INK)
    rounded((27, 45, 37, 58), 2, INK)
    rounded((29, 46, 35, 57), 1, RIM)
    polygon([(26, 54), (38, 54), (48, 58), (49, 61), (15, 61), (16, 58)], INK)
    polygon([(27, 54), (37, 54), (46, 58), (18, 58)], SILVER)

    rounded((13, 2, 51, 51), 8, INK)
    rounded((15, 4, 49, 49), 6, RIM)
    rounded((17, 5, 47, 47), 4, BLUE)
    rounded((16, 5, 48, 29), 4, SILVER)
    if small:
        for y in (11, 19):
            rounded((21, y, 43, y + 3), 1, INK)
    else:
        for y in (9, 13, 17, 21, 25):
            for x in range(20 + (2 if y in (13, 21) else 0), 45, 4):
                rounded((x, y, x + 2, y + 1.4), 0.6, "#506a7e")

    oval((25, 31, 39, 45), INK)
    oval((26, 32, 38, 44), SILVER)
    oval((28, 34, 36, 42), KNOB)
    if not small:
        line([(32, 38), (35, 35)], BLUE, 1)

    if tray and state in ("Paused", "Disconnected"):
        # A badge with a true gap stays legible on light and dark taskbars.
        oval((29, 31, 64, 66), (0, 0, 0, 0))
        oval((32, 34, 62, 64), INK)
        oval((34, 36, 60, 62), "#f5cc79" if state == "Paused" else "#d5dfe6")
        if state == "Paused":
            rounded((40, 42, 45, 56), 1, INK)
            rounded((49, 42, 54, 56), 1, INK)
        else:
            line([(41, 43), (53, 55)], INK, 4)
            line([(53, 43), (41, 55)], INK, 4)
    return img.resize((size, size), Image.Resampling.LANCZOS)


def encode_frame(img):
    w, h = img.size
    if w == 256:
        stream = BytesIO()
        img.save(stream, format="PNG", optimize=True)
        return stream.getvalue()
    # Classic ICO DIBs use bottom-up B,G,R,A plus a DWORD-aligned AND mask.
    rows = img.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    bitmap = rows.tobytes("raw", "BGRA")
    mask_stride = (w + 31) // 32 * 4
    mask = bytearray(mask_stride * h)
    alpha = rows.getchannel("A")
    for y in range(h):
        for x in range(w):
            if alpha.getpixel((x, y)) == 0:
                mask[y * mask_stride + x // 8] |= 0x80 >> (x % 8)
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(bitmap), 0, 0, 0, 0)
    return header + bitmap + mask


def write_icon(filename, state, sizes):
    frames = [encode_frame(draw_icon(size, state)) for size in sizes]
    offset = 6 + len(sizes) * 16
    directory = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
    for size, frame in zip(sizes, frames):
        directory.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(frame), offset))
        offset += len(frame)
    (OUT / filename).write_bytes(directory + b"".join(frames))


if __name__ == "__main__":
    write_icon("AraControl.ico", "Application", [16, 20, 24, 32, 40, 48, 64, 128, 256])
    for status in ("Connected", "Paused", "Disconnected"):
        write_icon("Tray" + status + ".ico", status, [16, 20, 24, 32, 40, 48, 64])
