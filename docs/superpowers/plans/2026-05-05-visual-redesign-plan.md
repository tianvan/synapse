# Visual Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the Synapse GitHub Pages site with "金棕典雅" editorial aesthetic — gold-brown palette, Georgia serif typography, three-tier score badges, podium + list layout, and pronounced hover effects.

**Architecture:** Single cohesive visual update across 5 files in `pages/`. The SCSS is the foundation; all templates reference its class names. Changes are purely presentational — no Liquid logic changes beyond adding score-tier CSS classes and splitting top 3 into podium.

**Tech Stack:** SCSS, Jekyll Liquid templates, zero external dependencies

---

### Task 1: Complete Visual Redesign (All Files)

**Files:**
- Rewrite: `pages/assets/css/style.scss` (full rewrite)
- Modify: `pages/_layouts/default.html` (header structure)
- Modify: `pages/index.html` (podium + list split)
- Modify: `pages/_includes/item-card.html` (score tier classes)
- Modify: `pages/_includes/podium-card.html` (hover effects)

- [ ] **Step 1: Rewrite `pages/assets/css/style.scss`**

```scss
---
---

// ============================================================
// Synapse Daily Digest — 金棕典雅
// ============================================================

// ---- Palette ----
$bg: #faf8f5;
$white: #fff;
$text: #2d1b0e;
$text-secondary: #5c3d20;
$muted: #8c6d51;
$gold: #daa520;
$gold-dark: #b8860b;
$gold-light: #f5e6c8;
$gold-pale: #f5ede0;
$silver: #c8bfb0;
$bronze: #cd7f32;
$border: #e8e0d5;
$border-light: #e0dcd5;
$accent: #c2410c;

// Tag colors
$tag-article-bg: #e8f0fe;
$tag-article-fg: #1e6091;
$tag-framework-bg: #e6f4ea;
$tag-framework-fg: #166534;
$tag-library-bg: #f3e8ff;
$tag-library-fg: #6b21a8;
$tag-tool-bg: #fce4e4;
$tag-tool-fg: #991b1b;
$tag-other-bg: #f3f0ec;
$tag-other-fg: #6b5e4a;
$tag-tech-bg: #f0ece0;

// ---- Font Stacks ----
$font-display: Georgia, "Noto Serif SC", "Source Han Serif SC", serif;
$font-body: "Noto Serif SC", "Source Han Serif SC", "Songti SC", "SimSun", serif;
$font-ui: system-ui, -apple-system, "Segoe UI", "PingFang SC", "Microsoft YaHei", sans-serif;

// ---- Reset ----
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

// ---- Base ----
body {
  font-family: $font-body;
  background: $bg;
  color: $text;
  line-height: 1.7;
  -webkit-font-smoothing: antialiased;
}

.container {
  max-width: 780px;
  margin: 0 auto;
  padding: 0 24px;
}

// ---- Header ----
.site-header {
  background: $white;
  border-bottom: 1px solid $border;
  padding: 28px 0;

  .container {
    display: flex;
    align-items: baseline;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 10px;
  }
}

.site-title {
  font-family: $font-display;
  font-size: 22px;
  font-weight: 700;
  letter-spacing: 0.01em;
  color: $text;

  a { color: inherit; text-decoration: none; }
  span { color: $gold; font-style: italic; }
}

.site-desc {
  font-size: 13px;
  color: $muted;
}

// ---- Digest Meta ----
.digest-meta {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
  margin-bottom: 28px;
}

.digest-date {
  font-family: $font-display;
  font-size: 15px;
  font-weight: 600;
  color: $text-secondary;
}

.digest-summary {
  font-size: 13px;
  color: $muted;
  font-style: italic;
}

// ---- Podium ----
.podium {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 14px;
  margin-bottom: 40px;
}

.podium-card {
  background: $white;
  border: 1px solid $border;
  border-radius: 10px;
  padding: 20px 18px;
  position: relative;
  overflow: hidden;
  box-shadow: 0 2px 8px rgba(0,0,0,0.04), 0 1px 2px rgba(0,0,0,0.03);
  transition: all 0.25s ease;
  cursor: pointer;

  // Top border colors by rank
  &:nth-child(1) { border-top: 3px solid $gold; }
  &:nth-child(2) { border-top: 3px solid #b0b0b0; }
  &:nth-child(3) { border-top: 3px solid $bronze; }

  // Hover
  &:hover {
    transform: translateY(-4px) scale(1.02);
    border-color: $gold;

    &:nth-child(1) {
      box-shadow: 0 8px 30px rgba(184,134,11,0.18), 0 4px 12px rgba(0,0,0,0.06);
      &::before {
        content: '';
        position: absolute;
        top: 0; left: 0; right: 0;
        height: 3px;
        background: linear-gradient(90deg, $gold, #f0d060, $gold);
      }
    }
    &:nth-child(2) {
      box-shadow: 0 8px 30px rgba(0,0,0,0.1), 0 4px 12px rgba(0,0,0,0.04);
    }
    &:nth-child(3) {
      box-shadow: 0 8px 30px rgba(205,127,50,0.14), 0 4px 12px rgba(0,0,0,0.04);
    }

    .podium-title a { color: $gold-dark; }
  }
}

.podium-rank {
  font-family: $font-ui;
  font-size: 10px;
  font-weight: 600;
  letter-spacing: 0.08em;
  margin-bottom: 4px;
}

.podium-title {
  font-family: $font-display;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.45;
  margin-bottom: 8px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;

  a {
    color: $text;
    text-decoration: none;
    transition: color 0.2s ease;
  }
}

.podium-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
}

.podium-score {
  position: absolute;
  top: 12px;
  right: 14px;
  font-family: $font-display;
  font-size: 28px;
  font-weight: 700;
  opacity: 0.12;
}

// ---- Section Header ----
.section-header {
  font-family: $font-ui;
  font-size: 12px;
  font-weight: 600;
  color: $muted;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  margin-bottom: 8px;
  display: inline-block;
  border-bottom: 2px solid $gold;
  padding-bottom: 4px;
}

// ---- Item Card (List) ----
.item-card {
  display: flex;
  gap: 14px;
  align-items: flex-start;
  padding: 14px 10px;
  background: $white;
  border: 1px solid $border;
  border-radius: 6px;
  margin-bottom: 8px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.03);
  transition: all 0.2s ease;
  cursor: pointer;

  // Score tiers — left border accent
  &.tier-gold {
    border-left: 3px solid $gold;
    background: linear-gradient(90deg, rgba(218,165,32,0.06), transparent 30%);
  }
  &.tier-silver {
    border-left: 2px solid $silver;
  }

  // Hover
  &:hover {
    transform: translateY(-2px);
    border-color: $gold;
    box-shadow: 0 4px 20px rgba(0,0,0,0.08);

    &.tier-gold {
      box-shadow: 0 4px 20px rgba(184,134,11,0.15);
      background: linear-gradient(90deg, rgba(218,165,32,0.1), rgba(218,165,32,0.02) 40%);
    }
    &:not(.tier-gold):not(.tier-silver) {
      background: linear-gradient(90deg, rgba(218,165,32,0.04), transparent 30%);
    }

    .card-title a { color: $gold-dark; }
    .rank-score { transform: scale(1.12); }
  }
}

// ---- Card Rank ----
.card-rank {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 42px;
}

.rank-number {
  font-family: $font-ui;
  font-size: 11px;
  color: #999;

  .tier-gold & { color: $gold-dark; font-weight: 600; }
}

.rank-score {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-top: 4px;
  width: 30px; height: 30px;
  border-radius: 50%;
  font-family: $font-display;
  font-size: 12px;
  font-weight: 700;
  transition: transform 0.2s ease;

  // Tier gold: solid gold circle
  .tier-gold & {
    background: $gold;
    color: $white;
  }
  // Tier silver: outlined gold
  .tier-silver & {
    background: $gold-pale;
    color: $muted;
    border: 1px solid $border;
    font-weight: 600;
  }
  // Tier neutral: muted outline
  &:not(.tier-gold .rank-score):not(.tier-silver .rank-score) {
    background: #f5f0e8;
    color: $muted;
    border: 1px solid $border-light;
    font-weight: 500;
  }
}

// ---- Card Body ----
.card-body {
  flex: 1;
  min-width: 0;
}

.card-title {
  font-family: $font-display;
  font-size: 14px;
  font-weight: 600;
  line-height: 1.5;

  a {
    color: $text;
    text-decoration: none;
    transition: color 0.2s ease;
  }
}

// ---- Tags ----
.card-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  align-items: center;
  margin-top: 5px;
}

.category-tag {
  display: inline-flex;
  padding: 2px 8px;
  border-radius: 3px;
  font-family: $font-ui;
  font-size: 10px;
  font-weight: 500;

  &.article  { background: $tag-article-bg; color: $tag-article-fg; }
  &.framework { background: $tag-framework-bg; color: $tag-framework-fg; }
  &.library  { background: $tag-library-bg; color: $tag-library-fg; }
  &.tool     { background: $tag-tool-bg; color: $tag-tool-fg; }
  &.other    { background: $tag-other-bg; color: $tag-other-fg; }
}

.tech-tag {
  display: inline-flex;
  padding: 2px 8px;
  border-radius: 3px;
  font-family: $font-ui;
  font-size: 10px;
  color: $muted;
  background: $tag-tech-bg;
  border: 1px solid $border;

  .tier-gold & {
    color: $muted;
  }
}

// ---- Suitability ----
.card-suitability {
  margin-top: 8px;
  font-size: 12px;
  color: $muted;
  line-height: 1.55;

  summary {
    cursor: pointer;
    font-family: $font-ui;
    font-size: 11px;
    font-weight: 500;
    color: $gold-dark;
    user-select: none;
    list-style: none;

    &::-webkit-details-marker { display: none; }

    &::after {
      content: ' ▾';
      font-size: 9px;
      opacity: 0.5;
    }
  }

  &[open] summary::after { content: ' ▴'; }

  p {
    margin-top: 6px;
    padding-left: 6px;
    border-left: 2px solid rgba($gold, 0.25);
  }
}

// ---- Empty State ----
.empty-state {
  text-align: center;
  padding: 100px 0;
  color: $muted;

  p {
    font-family: $font-display;
    font-size: 18px;
    font-style: italic;
    opacity: 0.6;
  }
}

// ---- Footer ----
.site-footer {
  margin-top: 64px;
  padding: 28px 0;
  text-align: center;
  border-top: 1px solid $border;

  p {
    font-size: 12px;
    color: $muted;

    a {
      color: $gold-dark;
      text-decoration: none;
      &:hover { text-decoration: underline; }
    }
  }
}

// ---- Responsive ----
@media (max-width: 680px) {
  .site-header {
    padding: 20px 0;
    .container { flex-direction: column; }
  }

  .site-title { font-size: 20px; }

  .podium {
    grid-template-columns: 1fr;
    gap: 10px;
  }

  .item-card {
    &:hover { transform: translateY(-1px); }
  }

  .card-title { font-size: 13px; }

  .digest-meta {
    flex-direction: column;
    align-items: flex-start;
    gap: 4px;
  }
}
```

- [ ] **Step 2: Verify SCSS compiles**

Since SCSS compilation requires Jekyll, verify syntax with a quick check:

```bash
# Check SCSS syntax with any Sass compiler available
# If sass CLI not available, syntax errors will be caught at Jekyll build time
echo "SCSS syntax check — will be validated at deploy time by GitHub Pages Jekyll build"
```

- [ ] **Step 3: Update `pages/_layouts/default.html`**

```html
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{{ site.title }}</title>
  <meta name="description" content="{{ site.description }}">
  <link rel="stylesheet" href="{{ '/assets/css/style.css' | relative_url }}">
</head>
<body>
  <header class="site-header">
    <div class="container">
      <h1 class="site-title"><a href="{{ '/' | relative_url }}">Synapse <span>Daily</span></a></h1>
      <p class="site-desc">AI 策展 · 技术资讯日报</p>
    </div>
  </header>
  <main class="container" style="padding-top: 32px;">
    {{ content }}
  </main>
  <footer class="site-footer">
    <div class="container">
      <p>Powered by <a href="https://github.com/tianvan/synapse">Synapse</a> &middot; AI-curated daily tech intelligence</p>
    </div>
  </footer>
</body>
</html>
```

- [ ] **Step 4: Update `pages/index.html`**

```html
---
layout: default
---

{% if site.data.digest %}
  {% assign sorted_items = site.data.digest.Items | sort: "Score" | reverse %}
  {% assign top3 = sorted_items | slice: 0, 3 %}
  {% assign rest = sorted_items | slice: 3, 100 %}

  <div class="digest-meta">
    <p class="digest-date">{{ site.data.digest.Id }}</p>
    <p class="digest-summary">{{ site.data.digest.Summary }}</p>
  </div>

  <!-- Top 3 Podium -->
  <div class="podium">
    {% for item in top3 %}
      {% include podium-card.html item=item rank=forloop.index %}
    {% endfor %}
  </div>

  <!-- Remaining List -->
  {% if rest.size > 0 %}
  <h2 class="section-header">更多资讯</h2>
  <div class="items-list">
    {% for item in rest %}
      {% assign actual_rank = forloop.index | plus: 3 %}
      {% include item-card.html item=item rank=actual_rank %}
    {% endfor %}
  </div>
  {% endif %}
{% else %}
  <div class="empty-state">
    <p>暂无日报数据，请稍后再来。</p>
  </div>
{% endif %}
```

- [ ] **Step 5: Update `pages/_includes/item-card.html`**

Score tier logic: `score >= 9` → `tier-gold`, `score >= 7` → `tier-silver`, else no tier class.

```html
{% assign score = include.item.Score %}
{% if score >= 9 %}
  {% assign tier_class = "tier-gold" %}
{% elsif score >= 7 %}
  {% assign tier_class = "tier-silver" %}
{% else %}
  {% assign tier_class = "" %}
{% endif %}

<article class="item-card {{ tier_class }}" data-score="{{ score }}">
  <div class="card-rank">
    <span class="rank-number">#{{ include.rank }}</span>
    <span class="rank-score">{{ score }}</span>
  </div>

  <div class="card-body">
    {% assign source = include.item.SourceRef.Value %}
    {% if source contains "github:" %}
      {% assign url = source | remove_first: "github:" | split: "/" %}
      {% assign url_final = "https://github.com/" | append: url[0] | append: "/" | append: url[1] %}
    {% elsif source contains "hn:" %}
      {% assign hn_id = source | remove_first: "hn:" %}
      {% assign url_final = "https://news.ycombinator.com/item?id=" | append: hn_id %}
    {% else %}
      {% assign url_final = "#" %}
    {% endif %}

    <h2 class="card-title">
      <a href="{{ url_final }}" target="_blank" rel="noopener noreferrer">{{ include.item.Highlight.Text }}</a>
    </h2>

    <div class="card-meta">
      <span class="category-tag {{ include.item.Category }}">{{ include.item.Category }}</span>
      {% for tag in include.item.TechStack.Tags %}
        <span class="tech-tag">{{ tag }}</span>
      {% endfor %}
    </div>

    {% if include.item.Suitability %}
    <details class="card-suitability">
      <summary>适用场景</summary>
      <p>{{ include.item.Suitability }}</p>
    </details>
    {% endif %}
  </div>
</article>
```

- [ ] **Step 6: Update `pages/_includes/podium-card.html`**

```html
{% assign score = include.item.Score %}
{% assign source = include.item.SourceRef.Value %}
{% if source contains "github:" %}
  {% assign url = source | remove_first: "github:" | split: "/" %}
  {% assign url_final = "https://github.com/" | append: url[0] | append: "/" | append: url[1] %}
{% elsif source contains "hn:" %}
  {% assign hn_id = source | remove_first: "hn:" %}
  {% assign url_final = "https://news.ycombinator.com/item?id=" | append: hn_id %}
{% else %}
  {% assign url_final = "#" %}
{% endif %}

<article class="podium-card">
  <p class="podium-rank">
    {% if include.rank == 1 %}🥇 TOP 1
    {% elsif include.rank == 2 %}🥈 TOP 2
    {% else %}🥉 TOP 3
    {% endif %}
  </p>

  <h2 class="podium-title">
    <a href="{{ url_final }}" target="_blank" rel="noopener noreferrer">{{ include.item.Highlight.Text }}</a>
  </h2>

  <div class="podium-meta">
    <span class="category-tag {{ include.item.Category }}">{{ include.item.Category }}</span>
    {% for tag in include.item.TechStack.Tags limit: 3 %}
      <span class="tech-tag">{{ tag }}</span>
    {% endfor %}
  </div>

  <span class="podium-score">{{ score }}</span>
</article>
```

- [ ] **Step 7: Verify with a Jekyll dry-run or quick check**

Since we can't run Jekyll locally without Ruby, verify the Liquid syntax has no obvious issues:
- All `{% %}` and `{{ }}` are properly closed
- All `include` calls pass the correct parameters
- All SCSS class names referenced in HTML exist in style.scss

- [ ] **Step 8: Commit all changes**

```bash
git add pages/
git commit -m "style: gold-brown editorial redesign with score tiers and hover effects"
```

- [ ] **Step 9: Push and deploy**

```bash
git push origin master
gh workflow run deploy-pages.yml
```
