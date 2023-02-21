---
layout: page
permalink: /submit/
title: Submit
description: Overview and general guidelines to submit a contribution to Computo
order: 3
nav: true
---

---

## Overview


Submissions to Computo require both scientific content (typically
equations, codes and figures, data) and a proof that this content is
reproducible. This is achieved by means of i) a notebook system, ii) a
virtual environment fixing the dependencies and iii) continuous
integration (plus, if needed, an external website to store large data
files such a Zenodo or OSF ).

A Computo submission is thus a git(hub) repository typically containing

- the source file of the notebook (a markdown  file with yaml metadata)
- auxiliary  files: a $$\mathrm{bib}\TeX  $$ file and some statics files, e.g. figures or small .csv data tables
- configuration files to set up the dependencies in a virtual environment
- configuration files to set up the continuous integration rendering the final documents

The compiled notebook (both HTML and PDF) will be generated directly
in the git(hub) repository via continuous integration (e.g., Github
action or Gitlab CI) and published, if the action is successful, to
web page (e.g. gh-page).

The PDF and the git repository address are then submitted
to    the   <a    href="https://computo.scholasticahq.com/for-authors"
style="outline: none; border:  none;">Computo submission platform</a>.

More details can be found in the following templates, which serve
as material for starting to write your submission, and as a
documentation for doing so. The [process is described in a
dedicated post]({{ site.baseurl }}/blog/2021/submission-process/).

<div class="info-block">
    <div class="info-block-header">Warning!</div>
     <div class="info-block-body">
	 <p>To start writing your own contribution, <em>do not start from scratch!!</em> You must clone the github repository associated with one of the following templates. Then, rename it an share it on your own github account.</p>
    </div>
</div>

## Available templates

### Quarto-based templates

Choosing one of [our quarto-based extension](https://github.com/computorg/computo-quarto-extension) would help us in the final formatting process of your article.  Moreover, it supports both R, Python and Julia!

We have templates for setting everything up either for R, Python and Julia users:

<div class="publications">

{% bibliography --file templates %}

</div>

### Other templates

if you are attached to Jupyter book or do not prefer to use Quarto, you are of course still encouraged to submit to Computo! A Jupyter-myst template is available that requires more formatting work for production, but author comfort is a priority.

<div class="publications">

{% bibliography --file templates_obsolete %}

</div>

## Large Files

If your submission materials contain files larger than 100MB, they won’t fit on a git repository as is. For this reason, we encourage you to put your data or any materials you deem necessary on a “open data” centered repository hub like [Zenodo](https://zenodo.org/). 

## Submit your work

Once your are happy with your notebook AND the github-action is
successful, you may submit your PDF via Scholastica, our platform for
peer-reviewing:

<div id="scholastica-submission-button" style="margin-top: 10px; margin-bottom: 10px;"><a href="https://computo.scholasticahq.com/for-authors" style="outline: none; border: none;"><img style="outline: none; border: none;" src="https://s3.amazonaws.com/docs.scholastica/law-review-submission-button/submit_via_scholastica.png" alt="Submit to Computo"></a></div>


## Reviewing and publication

Submitted papers are reviewed by external reviewers selected by the Associate Editor in charge of the paper.
Computo strives for fast reviewing cycles, but cannot provide strict guarantees on the matter; the current time between submission and publication is under six months.

In order to ensure an efficient reviewing process, authors are requested upon submission to suggest the names of four potential referees.  To avoid conflicts of interests, recent co-authors or collaborators should not be suggested.

Computo's accepted papers are published electronically immediately upon receipt under CC BY 4.0 license.
Authors retain the copyright and full publishing rights without restrictions.

More details about the reviewing process are available on the [Review page]({{ site.baseurl }} /review)

## Computo's code of ethics for authors

- **Originality**:
  Authors guarantee that their proposed article is original, and that it infringes no moral intellectual property right of any other person or entity.
  Authors guarantee that their proposed article has not been published previously, and that they have not submitted the proposed article simultaneously to any other journal.
- **Conflicts of interest**:
  Authors shall disclose any potential conflict of interest, whether it is professional,
  financial or other, to the journal’s Editor, if this conflict could be interpreted as having
  influenced their work. Authors shall declare all sources of funding for the research     presented in the article.
-  **Impartiality**:
  All articles are examined impartially, and their merits are assessed regardless of the
  sex, religion, sexual orientation, nationality, ethnic origin, length of service or     institutional affiliation of the author(s).
- **Funding**:
  All funding received by the author(s) shall be clearly stated in the article(s).
- **Defamatory statements**:
  Authors guarantee that their proposed article contains no matter of a defamatory, hateful, fraudulent or knowingly inexact character.
-  **References**:
  Authors guarantee that all the publications used in their work have been cited appropriately.
- **Copyright/author's right/license compliance**:
  Authors guarantee that they comply with the usage license of any third party contents/works (code, software, data, figures/images, documents, etc.) that were used to produce their work.

