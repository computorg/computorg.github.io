import os
import re
import shutil
import yaml
import glob
from datetime import datetime

def convert_jekyll_to_quarto(input_file, output_file):
    """Convert a Jekyll markdown file to Quarto format."""
    with open(input_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Extract Jekyll front matter
    front_matter_match = re.match(r'^---\s+(.*?)\s+---\s+(.*)', content, re.DOTALL)
    
    if (front_matter_match):
        front_matter_text = front_matter_match.group(1)
        post_content = front_matter_match.group(2)
        
        # Parse YAML front matter
        front_matter = yaml.safe_load(front_matter_text)
        
        # Create Quarto front matter
        quarto_front_matter = {}
        
        # Copy essential fields
        if 'title' in front_matter:
            quarto_front_matter['title'] = front_matter['title']
        
        if 'date' in front_matter:
            date_obj = front_matter['date']
            if isinstance(date_obj, datetime):
                quarto_front_matter['date'] = date_obj.strftime('%Y-%m-%d')
            else:
                quarto_front_matter['date'] = str(date_obj)
        
        if 'author' in front_matter:
            quarto_front_matter['author'] = front_matter['author']
        
        if 'description' in front_matter:
            quarto_front_matter['description'] = front_matter['description']
        
        # Ensure categories is always an array
        if 'categories' in front_matter:
            if isinstance(front_matter['categories'], str):
                quarto_front_matter['categories'] = [front_matter['categories']]
            else:
                quarto_front_matter['categories'] = front_matter['categories']
        
        # Ensure tags is always an array
        if 'tags' in front_matter:
            if isinstance(front_matter['tags'], str):
                quarto_front_matter['tags'] = [front_matter['tags']]
            else:
                quarto_front_matter['tags'] = front_matter['tags']
        
        if 'image' in front_matter:
            quarto_front_matter['image'] = front_matter['image']
        
        # Layout handling
        if 'layout' in front_matter:
            layout = front_matter['layout']
            if layout == 'distill':
                quarto_front_matter['format'] = {
                    'html': {
                        'code-fold': True,
                        'code-tools': True,
                        'code-link': True,
                        'df-print': 'paged',
                        'toc': True
                    }
                }
            elif layout == 'post':
                quarto_front_matter['format'] = {
                    'html': {
                        'toc': True
                    }
                }
            else:
                # Default format
                quarto_front_matter['format'] = {'html': {}}
        else:
            # Default format
            quarto_front_matter['format'] = {'html': {}}
        
        # Handle additional metadata
        if 'bibliography' in front_matter:
            quarto_front_matter['bibliography'] = front_matter['bibliography']
        
        if 'citation' in front_matter:
            quarto_front_matter['citation'] = front_matter['citation']
        
        # Handle social sharing metadata
        if 'share' in front_matter and front_matter['share']:
            quarto_front_matter['share'] = front_matter['share']
        
        # Page layout
        if 'page-layout' not in quarto_front_matter:
            quarto_front_matter['page-layout'] = 'article'
        
        # Handle permalinks more carefully to avoid duplicate aliases
        if 'permalink' in front_matter:
            permalink = front_matter['permalink'].rstrip('/')
            output_path = output_file.replace('\\', '/').split('/')
            
            # Get the current path without extension
            file_name = os.path.basename(output_file)
            dir_name = os.path.dirname(output_file)
            is_index = file_name == 'index.qmd'
            
            # Special handling for root index.qmd (skip all aliases)
            if is_index and dir_name == '' or dir_name == '.' or dir_name.endswith('/'):
                # This is the root index.qmd - don't add any aliases to avoid conflicts
                pass
            else:
                # Calculate the URL path this file will generate
                if is_index:
                    # For index.qmd files, the URL is the directory path
                    current_url = '/' + os.path.basename(dir_name)
                    if current_url == '/':  # Handle root index.qmd
                        current_url = '/'
                else:
                    # For non-index files, the URL includes the filename (minus extension)
                    current_url = '/' + os.path.join(os.path.basename(dir_name), 
                                                os.path.splitext(file_name)[0])
                
                # Only add alias if it would NOT cause a conflict with default URL path
                if permalink and permalink != '/' and permalink != '' and permalink != current_url and permalink != current_url + '/':
                    quarto_front_matter['aliases'] = [permalink]
        
        # Generate Quarto content
        quarto_content = "---\n" + yaml.dump(quarto_front_matter, sort_keys=False) + "---\n\n" + post_content
        
        # Fix YAML for listing exclude syntax
        quarto_content = fix_listing_exclude_syntax(quarto_content)
        
        # Replace Jekyll-specific syntax with Quarto equivalents
        # Replace liquid includes with Quarto includes
        quarto_content = re.sub(r'{%\s*include\s+([^%]+)\s*%}', r'{{< include /assets/_includes/\1 >}}', quarto_content)
        
        # Replace repository/repo.html specifically
        quarto_content = re.sub(r'{{< include /assets/_includes/repository/repo.html >}}', 
                             r'{{< include /assets/_includes/repo-list.html >}}', quarto_content)
        
        # Replace figure tags
        quarto_content = re.sub(r'{%\s*figure\s+([^%]+)\s*%}', r'![]({{ \1 }})', quarto_content)
        
        # Replace Jekyll bibliography tags
        quarto_content = re.sub(
            r'{%\s*bibliography\s+-f\s+([^\s]+)\s+-q\s+@\*\s+\[year={{y}}\]\*\s*%}',
            r'@* [year={{y}}]',
            quarto_content
        )
        
        # Check for any other potential include errors
        include_matches = re.findall(r'{{< include ([^>]+) >}}', quarto_content)
        for include in include_matches:
            include_path = include.strip()
            # If the path doesn't start with a slash, add the assets/_includes prefix
            if not include_path.startswith('/'):
                corrected_path = f'/assets/_includes/{include_path}'
                quarto_content = quarto_content.replace(f'{{{{ include {include} }}}}', 
                                                      f'{{{{ include {corrected_path} }}}}')
        
        # Write to output file
        with open(output_file, 'w', encoding='utf-8') as f:
            f.write(quarto_content)
        
        return True, front_matter
    else:
        print(f"Warning: Could not find front matter in {input_file}")
        return False, None

def fix_listing_exclude_syntax(content):
    """Fix the listing exclude syntax to use the proper Quarto format."""
    # Match YAML front matter
    front_matter_match = re.match(r'^---\s+(.*?)\s+---\s+(.*)', content, re.DOTALL)
    
    if not front_matter_match:
        return content
    
    front_matter_text = front_matter_match.group(1)
    post_content = front_matter_match.group(2)
    
    # Look for listing with exclude pattern
    exclude_pattern = re.compile(r'(listing:\s+.*?exclude:\s+)(-\s+.*?)([\n\r](?:\s*[^\s-]|\s*$))', re.DOTALL)
    
    if exclude_pattern.search(front_matter_text):
        # Found listing with exclude in incorrect format, fix it
        def replace_exclude(match):
            indent = len(re.match(r'^(.*?)exclude:', match.group(1)).group(1))
            indent_str = ' ' * indent
            
            # Extract the excluded items
            exclude_items = re.findall(r'-\s+(.*?)[\n\r]', match.group(2))
            
            # Format as files: [item1, item2, ...]
            if len(exclude_items) == 1:
                return f"{match.group(1)}files: [{exclude_items[0]}]{match.group(3)}"
            else:
                items_str = ', '.join(exclude_items)
                return f"{match.group(1)}files: [{items_str}]{match.group(3)}"
        
        fixed_front_matter = exclude_pattern.sub(replace_exclude, front_matter_text)
        return f"---\n{fixed_front_matter}---\n\n{post_content}"
    
    return content

def migrate_jekyll_directory(source_dir, target_dir):
    """Migrate all markdown files from source_dir to target_dir."""
    os.makedirs(target_dir, exist_ok=True)
    
    # Find all markdown files
    md_files = glob.glob(os.path.join(source_dir, "**", "*.md"), recursive=True)
    
    for md_file in md_files:
        # Extract filename and create path for new file
        filename = os.path.basename(md_file)
        
        # Replace date format from YYYY-MM-DD-title.md to title/index.qmd
        date_title_match = re.match(r'(\d{4}-\d{2}-\d{2})-(.*?)\.md$', filename)
        if date_title_match:
            date = date_title_match.group(1)
            title_slug = date_title_match.group(2)
            new_dir = os.path.join(target_dir, title_slug)
            os.makedirs(new_dir, exist_ok=True)
            output_file = os.path.join(new_dir, "index.qmd")
        else:
            # If not a dated post, just convert the filename
            name_without_ext = os.path.splitext(filename)[0]
            new_dir = os.path.join(target_dir, name_without_ext)
            os.makedirs(new_dir, exist_ok=True)
            output_file = os.path.join(new_dir, "index.qmd")
        
        # Convert the file
        convert_jekyll_to_quarto(md_file, output_file)
        
        # Copy any associated images or resources
        md_dir = os.path.dirname(md_file)
        for resource in os.listdir(md_dir):
            resource_path = os.path.join(md_dir, resource)
            if os.path.isfile(resource_path) and not resource.endswith('.md'):
                shutil.copy2(resource_path, new_dir)

def migrate_pages():
    """Migrate Jekyll _pages to Quarto pages and generate navigation structure."""
    if os.path.exists('_pages'):
        pages_dir = '_pages'
        md_files = glob.glob(os.path.join(pages_dir, "**", "*.md"), recursive=True)
        
        # Dictionary to store navigation structure
        nav_structure = []
        
        for md_file in md_files:
            # Get relative path from _pages
            rel_path = os.path.relpath(md_file, pages_dir)
            # Remove .md extension
            rel_path_no_ext = os.path.splitext(rel_path)[0]
            
            # Special case for home.md - make it the main index
            if rel_path_no_ext == 'home':
                output_file = 'index.qmd'
            else:
                # Create directory structure if needed
                dir_path = os.path.dirname(rel_path_no_ext)
                if dir_path:
                    os.makedirs(dir_path, exist_ok=True)
                
                # Create output file path
                if rel_path_no_ext.endswith('index'):
                    output_file = f"{rel_path_no_ext}.qmd"
                else:
                    # Create directory for the page
                    os.makedirs(rel_path_no_ext, exist_ok=True)
                    output_file = f"{rel_path_no_ext}/index.qmd"
            
            # Convert the file and get front matter
            success, front_matter = convert_jekyll_to_quarto(md_file, output_file)
            
            # If conversion successful and file has navigation info, add to structure
            if success and front_matter:
                # Extract navigation information if available
                if 'permalink' in front_matter and 'title' in front_matter:
                    permalink = front_matter['permalink'].rstrip('/')
                    if permalink == '':
                        href = '/'
                    else:
                        href = permalink
                    
                    # Replace with corresponding Quarto path
                    if href == '/':
                        href = 'index.qmd'
                    else:
                        href = href.lstrip('/') + '.qmd'
                        # If it's not a direct file path, assume it's a directory with index.qmd
                        if not os.path.exists(href):
                            href = href.replace('.qmd', '/index.qmd')
                    
                    nav_item = {
                        'href': href,
                        'text': front_matter['title']
                    }
                    
                    # Handle nav_order if present
                    if 'nav_order' in front_matter:
                        nav_item['order'] = int(front_matter['nav_order'])
                    
                    nav_structure.append(nav_item)
        
        # Sort navigation items by order if present
        nav_structure.sort(key=lambda x: x.get('order', 999))
        
        # Remove 'order' field used for sorting
        for item in nav_structure:
            if 'order' in item:
                del item['order']
        
        # Update _quarto.yml with navigation structure
        update_quarto_navigation(nav_structure)

def update_quarto_navigation(nav_structure):
    """Update _quarto.yml with the navigation structure from _pages."""
    quarto_yml_path = '_quarto.yml'
    
    if os.path.exists(quarto_yml_path):
        with open(quarto_yml_path, 'r') as f:
            quarto_config = yaml.safe_load(f)
        
        # Update navigation
        if 'website' in quarto_config and 'navbar' in quarto_config['website']:
            # First clean up the nav structure to remove redundant 'about' entries
            # and rename main site entry
            for item in nav_structure:
                if item.get('href') == 'index.qmd' and item.get('text', '').lower() == 'about':
                    # This is the main site entry - update text to site name
                    item['text'] = quarto_config['website'].get('title', 'Computo.org')
            
            # Remove "Home" entry if it exists - we only want the main entry
            nav_structure = [item for item in nav_structure if not (
                item.get('href') == 'index.qmd' and 
                item.get('text', '').lower() == 'home'
            )]
            
            # Ensure blog entry exists
            blog_entry = {'href': 'blog/index.qmd', 'text': 'Blog'}
            if not any(item.get('href') == 'blog/index.qmd' for item in nav_structure):
                # Add blog entry after main site entry but before others
                if len(nav_structure) > 0:
                    nav_structure.insert(1, blog_entry)
                else:
                    nav_structure.append(blog_entry)
            
            # Remove redundant "About" entries if there's more than one
            about_entries = [i for i, item in enumerate(nav_structure) 
                            if item.get('text', '').lower() == 'about' 
                            and item.get('href') != 'index.qmd']
            
            # Keep only the last "About" entry if there are multiple
            if len(about_entries) > 1:
                for i in reversed(about_entries[:-1]):
                    del nav_structure[i]
            
            quarto_config['website']['navbar']['left'] = nav_structure
            
            with open(quarto_yml_path, 'w') as f:
                yaml.dump(quarto_config, f, sort_keys=False)
            
            print(f"Updated navigation structure in {quarto_yml_path}")

def migrate_layout_assets():
    """Migrate layout assets like CSS, JS, and images."""
    # Create directories
    os.makedirs('_extensions', exist_ok=True)
    
    # Create includes directory for Jekyll includes to be converted to Quarto
    includes_dir = 'assets/_includes'
    os.makedirs(includes_dir, exist_ok=True)
    
    # Create repo-list.html in the includes directory
    with open(os.path.join(includes_dir, 'repo-list.html'), 'w', encoding='utf-8') as f:
        f.write("""<div class="repo-list">
  <div class="repo">
    <h3><a href="https://github.com/computorg/computo-quarto-extension">computo-quarto-extension</a></h3>
    <p>Quarto extension for Computo journal articles</p>
    <div class="repo-meta">
      <span><i class="fas fa-code-branch"></i> Quarto</span>
      <span><i class="fas fa-star"></i> Extension</span>
    </div>
  </div>
  
  <div class="repo">
    <h3><a href="https://github.com/computorg/template">article-template</a></h3>
    <p>Template for Computo journal articles</p>
    <div class="repo-meta">
      <span><i class="fas fa-code-branch"></i> Template</span>
      <span><i class="fas fa-star"></i> Journal</span>
    </div>
  </div>
  
  <!-- Add more repositories as needed -->
</div>""")
    
    # Copy Jekyll includes if they exist
    if os.path.exists('_includes'):
        for include_file in glob.glob('_includes/**', recursive=True):
            if os.path.isfile(include_file):
                relative_path = os.path.relpath(include_file, '_includes')
                target_path = os.path.join(includes_dir, relative_path)
                os.makedirs(os.path.dirname(target_path), exist_ok=True)
                shutil.copy2(include_file, target_path)
                print(f"Copied include file {include_file} to {target_path}")
    
    # Copy assets directory if it exists
    if os.path.exists('assets'):
        if not os.path.exists('assets/scss'):
            os.makedirs('assets/scss', exist_ok=True)
        
        # Create light and dark theme files with proper Quarto SCSS layer boundaries
        with open('light.scss', 'w', encoding='utf-8') as f:
            f.write("""/*-- scss:defaults --*/
$primary: #4e7aff;
$navbar-bg: $primary;
$body-bg: #ffffff;
$body-color: #333333;

/*-- scss:rules --*/
// Any additional custom rules for the light theme
""")
        
        with open('dark.scss', 'w', encoding='utf-8') as f:
            f.write("""/*-- scss:defaults --*/
$primary: #4e7aff;
$navbar-bg: $primary;
$body-bg: #212529;
$body-color: #e9ecef;

/*-- scss:rules --*/
// Any additional custom rules for the dark theme
""")

def copy_assets():
    """Copy assets from Jekyll to Quarto structure."""
    # Create basic directories
    os.makedirs('assets/img', exist_ok=True)
    
    # Copy logo - prioritize SVG versions first, then fallback to other formats
    logo_found = False
    possible_logo_paths = [
        # SVG versions first
        'assets/img/logo.svg',
        'assets/img/logo_notext_white.svg',
        'assets/logo/logo.svg',
        'assets/images/logo.svg',
        '_assets/logo.svg',
        '_assets/img/logo.svg',
        'images/logo.svg',
        # PNG versions as fallback
        'assets/img/logo_notext_white.png',
        'assets/img/logo.png',
        'assets/logo/logo.png',
        'assets/images/logo.png',
        '_assets/logo.png',
        '_assets/img/logo.png',
        'images/logo.png',
    ]
    
    for logo_path in possible_logo_paths:
        if os.path.exists(logo_path):
            # Determine target extension based on source file
            source_ext = os.path.splitext(logo_path)[1]
            target_path = f"assets/img/logo{source_ext}"
            
            # Create the target directory
            os.makedirs(os.path.dirname(target_path), exist_ok=True)
            
            # Copy the logo to the standard location with proper extension
            shutil.copy2(logo_path, target_path)
            print(f"Copied logo from {logo_path} to {target_path}")
            
            # If found SVG, also update _quarto.yml to reference the SVG version
            if source_ext == '.svg' and os.path.exists('_quarto.yml'):
                try:
                    with open('_quarto.yml', 'r') as f:
                        quarto_config = yaml.safe_load(f)
                    
                    if 'website' in quarto_config and 'navbar' in quarto_config['website']:
                        quarto_config['website']['navbar']['logo'] = target_path
                        
                        with open('_quarto.yml', 'w') as f:
                            yaml.dump(quarto_config, f, sort_keys=False)
                        
                        print(f"Updated _quarto.yml to use SVG logo at {target_path}")
                except Exception as e:
                    print(f"Error updating _quarto.yml: {e}")
            
            # Also preserve the original file if it has a different name
            if os.path.basename(logo_path) != os.path.basename(target_path):
                preserve_path = f"assets/img/{os.path.basename(logo_path)}"
                # Only copy if source and destination paths are different
                if logo_path != preserve_path:
                    shutil.copy2(logo_path, preserve_path)
                    print(f"Also preserved original logo at {preserve_path}")
                else:
                    print(f"Original logo already at {preserve_path}, no need to copy")
            
            logo_found = True
            break
    
    if not logo_found:
        print("Warning: Could not find logo file to copy. Please manually copy your logo to assets/img/logo.svg or logo.png")
        print("Looked in the following locations: " + ", ".join(possible_logo_paths))
    
    # Copy all images from assets directory recursively if it exists
    # Prioritize SVG files by listing them first in the search patterns
    if os.path.exists('assets'):
        # Look for SVG files first, then other image formats
        for img_file in glob.glob('assets/**/*.svg', recursive=True) + \
                       glob.glob('assets/**/*.png', recursive=True) + \
                       glob.glob('assets/**/*.jpg', recursive=True) + \
                       glob.glob('assets/**/*.jpeg', recursive=True) + \
                       glob.glob('assets/**/*.gif', recursive=True):
            target_path = img_file
            os.makedirs(os.path.dirname(target_path), exist_ok=True)
            if not os.path.exists(target_path):
                shutil.copy2(img_file, target_path)
                print(f"Copied image {img_file} to {target_path}")

def create_index_redirects():
    """Create redirect files for common Jekyll paths."""
    # Create a redirects file for common Jekyll paths
    os.makedirs('redirects', exist_ok=True)
    with open('redirects/_redirects', 'w', encoding='utf-8') as f:
        f.write("""# Redirects from Jekyll paths to Quarto paths
/index.html              /
/pages/*                 /:splat
/tags/:tag               /blog/index.html?tag=:tag
/categories/:category    /blog/index.html?category=:category
""")

def fix_problematic_aliases():
    """Fix any existing files with problematic aliases that would cause conflicts."""
    # Special handling for root index.qmd
    if os.path.exists('index.qmd'):
        try:
            # Read the file content
            with open('index.qmd', 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Extract the frontmatter and content cleanly
            # First, look for the frontmatter delimiters
            if content.startswith('---'):
                # Find the closing --- delimiter
                end_idx = content.find('---', 3)
                if end_idx != -1:
                    # Extract frontmatter text
                    frontmatter_text = content[3:end_idx].strip()
                    
                    # Extract content after frontmatter (skipping any extra ---)
                    remaining_content = content[end_idx+3:].strip()
                    # Skip any additional --- at the start of remaining content
                    if remaining_content.startswith('---'):
                        remaining_content = remaining_content[3:].strip()
                    
                    # Parse frontmatter as YAML
                    frontmatter = yaml.safe_load(frontmatter_text)
                    
                    # Remove any aliases
                    if 'aliases' in frontmatter:
                        del frontmatter['aliases']
                    
                    # Write the clean document back
                    with open('index.qmd', 'w', encoding='utf-8') as f:
                        f.write('---\n')
                        yaml.dump(frontmatter, f, default_flow_style=False, sort_keys=False)
                        f.write('---\n\n')
                        f.write(remaining_content)
                    
                    print("Fixed index.qmd by directly handling YAML frontmatter")
            else:
                print("index.qmd doesn't start with a YAML frontmatter delimiter")
        except Exception as e:
            print(f"Error processing index.qmd: {e}")
            
    # Check all directories for index.qmd files that might have conflicting aliases
    for dir_path, _, files in os.walk('.'):
        if 'index.qmd' in files and dir_path != '.' and dir_path != './':
            file_path = os.path.join(dir_path, 'index.qmd')
            dir_name = os.path.basename(dir_path)
            
            with open(file_path, 'r') as f:
                content = f.read()
            
            # Check if this file has an alias to its own path
            alias_pattern = re.compile(rf'aliases:\s*\n\s*-\s*[\'"]?/{dir_name}/?[\'"]\s*', re.DOTALL)
            if alias_pattern.search(content):
                # Remove the problematic alias
                content = re.sub(rf'aliases:\s*\n\s*-\s*[\'"]?/{dir_name}/?[\'"]\s*', '', content)
                # Clean up any empty lines
                content = re.sub(r'\n\n+', '\n\n', content)
                
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(content)
                print(f"Fixed problematic aliases in {file_path}")

def create_listing_templates():
    """Create EJS templates for listings."""
    templates_dir = 'assets/listing-templates'
    os.makedirs(templates_dir, exist_ok=True)
    
    # Create the default listing template - note proper wrapping with {=html}
    with open(os.path.join(templates_dir, 'default.ejs'), 'w', encoding='utf-8') as f:
        f.write("""```{=html}
<% for (const item of items) { %>
<div class="quarto-listing-item" data-categories="<%= item.categories %>" data-listing-date-sort="<%= item['listing-date-sort'] %>">
  <div class="quarto-post-item">
    <div class="quarto-post-metadata">
      <div>
        <% if (item.date) { %>
        <p class="quarto-post-date"><%= item.date %></p>
        <% } %>
      </div>
    </div>
    <div class="quarto-post-body">
      <h3 class="no-anchor quarto-post-title">
        <a href="<%= item.path %>"><%= item.title %></a>
      </h3>
      <div>
        <div class="quarto-post-description">
          <%= item.description %>
        </div>
      </div>
      <% if (item.categories && item.categories.length > 0) { %>
      <div class="quarto-post-categories">
        <% for (const category of item.categories) { %>
        <div class="quarto-listing-category"><%= category %></div>
        <% } %>
      </div>
      <% } %>
    </div>
  </div>
</div>
<% } %>
```""")
    
    print(f"Created listing template in {templates_dir}/default.ejs")

def fix_listing_in_qmd_files():
    """Find all qmd files with listings and ensure they use the external EJS template and proper sorting."""
    listing_files = []
    
    # First, find all files with listings
    for qmd_file in glob.glob('**/*.qmd', recursive=True):
        with open(qmd_file, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Check if file contains listing in frontmatter
        if re.search(r'listing:\s*\n', content):
            listing_files.append(qmd_file)
    
    # Then update each file to use the external template
    for qmd_file in listing_files:
        with open(qmd_file, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Extract front matter
        front_matter_match = re.match(r'^---\s+(.*?)\s+---\s+(.*)', content, re.DOTALL)
        if front_matter_match:
            front_matter_text = front_matter_match.group(1)
            post_content = front_matter_match.group(2)
            
            # Parse YAML front matter
            front_matter = yaml.safe_load(front_matter_text)
            
            # Update listing configuration to use external template
            if 'listing' in front_matter:
                # Determine relative path to template
                rel_path = os.path.relpath('assets/listing-templates', os.path.dirname(qmd_file))
                template_path = os.path.join(rel_path, 'default.ejs').replace('\\', '/')
                
                # Add template to listing config if not already present
                front_matter['listing']['template'] = template_path
                
                # Ensure sorting is set to date desc (most recent first)
                front_matter['listing']['sort'] = "date desc"
                
                # Write back updated content
                updated_content = "---\n" + yaml.dump(front_matter, sort_keys=False) + "---\n\n" + post_content
                
                # Remove any embedded EJS templates
                updated_content = re.sub(r'\{%\s*for.*?endfor\s*%\}', '', updated_content, flags=re.DOTALL)
                
                with open(qmd_file, 'w', encoding='utf-8') as f:
                    f.write(updated_content)
                
                print(f"Updated listing template and sorting in {qmd_file}")

# First migrate the main layout assets
migrate_layout_assets()
copy_assets()

# Migrate pages FIRST to establish the navigation structure and main index
migrate_pages()

# Then migrate regular content
migrate_jekyll_directory('_posts', 'blog')
migrate_jekyll_directory('_news', 'news')

# Create redirects for common Jekyll paths
create_index_redirects()

# Create a sample project file to avoid the empty listing warning
if os.path.exists('_projects'):
    migrate_jekyll_directory('_projects', 'current')
    
    # Create a sample project if none exist to avoid empty listing warning
    current_files = glob.glob('current/**/*.qmd', recursive=True)
    if len(current_files) <= 1:  # Only index.qmd exists
        with open('current/sample-project/index.qmd', 'w') as f:
            f.write("""---
title: "Sample Project"
date: "2023-01-01"
description: "A sample project to demonstrate the listing capability"
categories: [sample]
---

# Sample Project

This is a placeholder project to demonstrate how projects are listed in Computo.
When you add actual projects, you can delete this sample.
""")
else:
    # Create current directory without a listing to avoid warnings
    os.makedirs('current', exist_ok=True)
    with open('current/index.qmd', 'w', encoding='utf-8') as f:
        f.write("""---
title: "Current Projects"
page-layout: full
---

## Our Current Projects

Computo is involved in several ongoing projects related to computational reproducibility and statistical software.

### Reproducibility Framework

We're developing standards and tools to ensure computational research can be easily reproduced.

### Template Development

Creating and maintaining templates that make it easy to prepare reproducible research papers.

### Community Engagement

Building a community of researchers committed to open and reproducible computational science.

<!-- When you have projects to list, uncomment the section below -->
<!--
:::{.callout-note}
## Project Listing
To add your project, create a new directory in the "current" folder with an index.qmd file.
:::
-->
""")

# Fix blog and news index files
if os.path.exists('blog/index.qmd'):
    with open('blog/index.qmd', 'r') as f:
        content = f.read()
    
    fixed_content = fix_listing_exclude_syntax(content)
    
    with open('blog/index.qmd', 'w') as f:
        f.write(fixed_content)

if os.path.exists('news/index.qmd'):
    with open('news/index.qmd', 'r') as f:
        content = f.read()
    
    fixed_content = fix_listing_exclude_syntax(content)
    
    with open('news/index.qmd', 'w') as f:
        f.write(fixed_content)

# Create about page if it doesn't exist
if not os.path.exists('about') and not os.path.exists('about/index.qmd'):
    os.makedirs('about', exist_ok=True)
    with open('about/index.qmd', 'w', encoding='utf-8') as f:
        f.write("""---
title: "About Computo.org"
page-layout: article
---

Information about our organization and mission.
""")

# Create publications page if it doesn't exist
if not os.path.exists('publications') and not os.path.exists('publications/index.qmd'):
    os.makedirs('publications', exist_ok=True)
    with open('publications/index.qmd', 'w', encoding='utf-8') as f:
        f.write("""---
title: "Publications"
page-layout: article
bibliography: references.bib
nocite: |
  @*
---

## Publications

::: {#refs}
:::
""")

# Fix any problematic aliases
fix_problematic_aliases()

# Create listing templates and fix listings in qmd files
create_listing_templates()
fix_listing_in_qmd_files()

# Create blog/index.qmd if it doesn't exist already
def create_blog_index():
    """Create blog index file if it doesn't exist."""
    if not os.path.exists('blog/index.qmd'):
        os.makedirs('blog', exist_ok=True)
        with open('blog/index.qmd', 'w', encoding='utf-8') as f:
            f.write("""---
title: "Computo Blog"
listing:
  contents: .
  sort: "date desc"  # Ensure sorting is from most recent to least recent
  type: default
  categories: true
  sort-ui: false
  filter-ui: false
  fields: [date, title, description, categories]
  feed: true
  template: ../assets/listing-templates/default.ejs
  exclude:
    files: [index.qmd]
page-layout: full
title-block-banner: true
---

Welcome to the Computo blog, where we share updates, tutorials, and information about computational reproducibility in research.
""")
        print("Created blog index file")

# Add to your migration process sequence, near the end:
# After fixing problematic aliases
create_listing_templates()
fix_listing_in_qmd_files()
create_blog_index()

def update_publications_index():
    """Update publications index.qmd to properly handle bibliographies using multibib."""
    # Create publications directory if it doesn't exist
    os.makedirs('publications', exist_ok=True)
    
    # Check for bibliography files in _bibliography directory
    bib_files = []
    if os.path.exists('_bibliography'):
        bib_files = glob.glob('_bibliography/*.bib')
        
        # Copy bibliography files to publications directory
        for bib_file in bib_files:
            target_path = os.path.join('publications', os.path.basename(bib_file))
            shutil.copy2(bib_file, target_path)
            print(f"Copied bibliography file {bib_file} to {target_path}")
    
    # If no bibliography files found, create sample ones
    if not bib_files:
        # Create published.bib
        published_bib = os.path.join('publications', 'published.bib')
        with open(published_bib, 'w', encoding='utf-8') as f:
            f.write("""@article{Doe2023,
  author = {Doe, John and Smith, Jane},
  title = {A Sample Article for Computo},
  journal = {Computo},
  year = {2023},
  volume = {1},
  number = {1},
  pages = {1--10},
  doi = {10.1234/sample.2023.001}
}""")
        
        # Create inpipeline.bib
        pipeline_bib = os.path.join('publications', 'inpipeline.bib')
        with open(pipeline_bib, 'w', encoding='utf-8') as f:
            f.write("""@article{Smith2023,
  author = {Smith, Robert and Johnson, Emily},
  title = {Reproducible Computational Methods},
  journal = {Journal of Statistical Software},
  year = {2023},
  volume = {100},
  number = {1},
  doi = {10.1234/jss.2023.100.1},
  note = {In Press}
}""")
        
        # Create examples.bib
        examples_bib = os.path.join('publications', 'examples.bib')
        with open(examples_bib, 'w', encoding='utf-8') as f:
            f.write("""@article{Example2023,
  author = {Example, Author},
  title = {Mock Contribution with Advanced Formatting},
  journal = {Computo},
  year = {2023},
  volume = {1},
  number = {2},
  url = {https://computo.sfds.asso.fr/published-article-1/},
  note = {Example Article}
}""")
        
        print(f"Created sample bibliography files in publications/")
        bib_files = [published_bib, pipeline_bib, examples_bib]
    
    # Create publications/index.qmd that uses multibib for multiple bibliographies
    with open('publications/index.qmd', 'w', encoding='utf-8') as f:
        f.write("""---
title: "Articles"
description: "Publications by years in reversed chronological order"
filters:
  - multibib
validate-yaml: false
bibliography:
  published: published.bib
  pipeline: in_production.bib
  examples: mock_papers.bib
nocite: |
  @*
format:
  html: 
    toc: true
page-layout: article
---

## Published Articles

::: {#refs-published}
:::

## In the Pipeline

Manuscripts conditionally accepted, whose editorial and scientific reproducibility are being validated.

::: {#refs-pipeline}
:::

## Examples and Mock Contributions

These are examples that help authors submitting to the journal by demonstrating formatting features.

::: {#refs-examples}
:::
""")
    print(f"Updated publications/index.qmd to use multibib extension with multiple bibliographies")

update_publications_index()

print("Migration completed. Please review the converted files for any issues.")
print("IMPORTANT: The navigation structure has been generated from the _pages directory.")
print("Review the _quarto.yml file to ensure the navigation is correct.")
print("Check all includes in your Quarto files to ensure they point to the correct paths.")
