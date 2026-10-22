import re
import sys

LIMITS = {
    "Название": 50,
    "Описание для SEO": 160,
    "Об игре": 1000,
    "Короткое описание": 70,
    "Как играть": 1000,
    "Ключевые слова": 100,
}
REQUIRED = {"Название", "Об игре", "Как играть"}
FORBIDDEN_SHORT = ("бесплатн", "на русском")


def parse(text):
    langs, lang, field = {}, None, None
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if line.startswith("## "):
            lang = line[3:].strip()
            langs[lang] = {}
        elif line.startswith("### ") and lang:
            field = line[4:].strip()
        elif line.strip().startswith("```text") and lang and field:
            body = []
            i += 1
            while i < len(lines) and lines[i].strip() != "```":
                body.append(lines[i])
                i += 1
            langs[lang][field] = "\n".join(body).strip()
            field = None
        i += 1
    return langs


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    path = sys.argv[1] if len(sys.argv) > 1 else "CodeDocs/YandexDraft.md"
    with open(path, encoding="utf-8") as f:
        langs = parse(f.read())

    problems = 0
    for lang, fields in langs.items():
        print(f"[{lang}]")
        name = fields.get("Название", "")
        seen = {}
        for field, limit in LIMITS.items():
            value = fields.get(field)
            if value is None:
                if field in REQUIRED:
                    print(f"  {field:<18} нет поля")
                    problems += 1
                continue
            n = len(value)
            status = "ok"
            if n == 0:
                status = "пусто"
            elif n > limit:
                status = "ПРЕВЫШЕН"
            elif value in seen:
                status = f"повтор поля «{seen[value]}»"
            elif field == "Короткое описание":
                low = value.lower()
                if any(w in low for w in FORBIDDEN_SHORT) or (name and name.lower() in low):
                    status = "запрещённые слова/название"
                elif value[0].islower() or (value[0] in "«\"" and value[-1] in "»\""):
                    status = "заглавная буква/кавычки"
            elif field == "Ключевые слова" and ", " in value:
                status = "пробелы после запятых"
            if status != "ok":
                problems += 1
            seen.setdefault(value, field)
            print(f"  {field:<18} {n:>4}/{limit:<4} {n * 100 // limit:>3}%  {status}")
        if re.search(r"https?://|www\.|\.(ru|com|io)\b", "\n".join(fields.values())):
            print("  ссылка или домен в тексте")
            problems += 1
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
