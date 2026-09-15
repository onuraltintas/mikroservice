# CMS marketing-language repair v1

This reversible maintenance pack adds two missing `AboutPage` CMS blocks. Before this pack, the public page rendered its application fallback, including a sentence that could be read as a direct outcome promise. The revised copy describes the actual programme flow: initial measurement, regular practice and a personalised plan.

The pack targets only `about_hero_subtitle` and `about_story_content`. `apply.sql` creates both blocks only when neither exists, and refuses to overwrite a later editor change. The new blocks are manageable from the CMS admin panel and the action is recorded in `speed_reading.admin_audit_records`.

Before applying in production, create a database snapshot. Use `rollback.sql` only when both blocks still have the revised values.
