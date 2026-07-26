#!/usr/bin/env lua
-- Post-render script to fix and enhance sitemap.xml
-- Handles two tasks:
-- 1. Fix Quarto's /index.html URLs to trailing-slash form
-- 2. Add published article URLs from published.yml to sitemap
--
-- Quarto bug: https://github.com/quarto-dev/quarto-cli/discussions/11398

local output_dir = os.getenv("QUARTO_PROJECT_OUTPUT_DIR") or "_site"
local sitemap_path = output_dir .. "/sitemap.xml"
local published_yml_path = "site/published.yml"

-- Read file contents
local function read_file(path)
  local file = io.open(path, "r")
  if not file then
    return nil
  end
  local content = file:read("*a")
  file:close()
  return content
end

-- Write content to file
local function write_file(path, content)
  local file = io.open(path, "w")
  if not file then
    error("Cannot write to " .. path)
  end
  file:write(content)
  file:close()
end

-- Extract article URLs from published.yml
-- Uses the repo: field (e.g., published-202402-elmasri-optimal) to construct URLs
local function extract_article_urls(yml_content)
  local urls = {}
  local seen = {}
  for line in yml_content:gmatch("[^\r\n]+") do
    local repo = line:match("^%s- repo: (published-[%w%-]+)")
    if repo and not seen[repo] then
      local url = "https://computo-journal.org/" .. repo .. "/"
      table.insert(urls, url)
      seen[repo] = true
    end
  end
  return urls
end

-- Check if URL already exists in sitemap
local function url_exists_in_sitemap(sitemap_content, url)
  return sitemap_content:find("<loc>" .. url .. "</loc>", 1, true) ~= nil
end

-- Main logic
local sitemap_content = read_file(sitemap_path)
if not sitemap_content then
  print("No sitemap.xml found, skipping")
  os.exit(0)
end

local modified = false

-- Task 1: Fix /index.html URLs to trailing-slash form
local fixed_content = sitemap_content:gsub("/index%.html</loc>", "/</loc>")
if fixed_content ~= sitemap_content then
  sitemap_content = fixed_content
  modified = true
  print("Fixed /index.html URLs to trailing-slash form")
end

-- Task 2: Add article URLs from published.yml
local yml_content = read_file(published_yml_path)
if yml_content then
  local article_urls = extract_article_urls(yml_content)
  local lastmod = os.date("!%Y-%m-%dT%H:%M:%SZ")
  
  for _, url in ipairs(article_urls) do
    if not url_exists_in_sitemap(sitemap_content, url) then
      -- Insert URL before </urlset>
      local entry = string.format(
        '  <url>\n    <loc>%s</loc>\n    <lastmod>%s</lastmod>\n  </url>\n',
        url, lastmod
      )
      sitemap_content = sitemap_content:gsub("</urlset>", entry .. "</urlset>")
      modified = true
    end
  end
  
  if #article_urls > 0 then
    print("Added " .. #article_urls .. " article URLs to sitemap")
  end
else
  print("No published.yml found, skipping article URLs")
end

-- Write updated sitemap
if modified then
  write_file(sitemap_path, sitemap_content)
  print("Updated sitemap.xml")
else
  print("No changes needed")
end
