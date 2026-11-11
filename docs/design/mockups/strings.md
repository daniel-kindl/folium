# User-facing strings in the mockups

These strings appear in the 2026-11-11 Folium visual mockups.
Sample book data (titles, authors, publishers, dates, ISBNs, edition notes and descriptions) is shown only as example content. Do not treat that sample data as product copy.

The sort labels use an en dash, as drawn in the mockups, for example "Title A–Z".

## Chrome and navigation

| String | Where it is used |
| --- | --- |
| Library | App bar title. Bottom tab. "Go to Library" uses the same word. |
| Find online | Bottom tab. App bar title on the online search screen. |
| Add book | Library button at the bottom right. TalkBack label when the button shows only the plus icon at 200 % font scale. |
| Add a book | Title of the add sheet. |
| Find a book online | Sheet row. Empty Library primary button. |
| Search Open Library and review the details | Sheet row caption under "Find a book online". |
| Add a book by hand | Sheet row. Empty Library secondary button. |
| Enter a title and an author | Sheet row caption under "Add a book by hand". |
| Backup and restore | Library overflow destination. Screen title. |
| Search saved books | TalkBack label for the Library search icon. |
| Show covers | TalkBack label for the list-to-grid view button. |
| Show list | TalkBack label for the grid-to-list view button. |
| More options | TalkBack label for the overflow (vertical dots) icon. |
| Back | TalkBack label for the back icon. |
| Cancel | TalkBack label for the close icon on Review and save and Restore backup. Button on Review and save. |
| Clear search | TalkBack label for the clear icon in saved-book search. |
| Main | TalkBack name of the bottom navigation bar (`aria-label`). |
| Reading status | TalkBack name of the status filter and of the details status selector (`aria-label`). Visible section label on book details. |

## Library

| String | Where it is used |
| --- | --- |
| All | Status filter segment. Also in "Status: All". |
| To read | Status filter segment. Status value on a book. Details status selector. |
| Reading | Status filter segment. Status value on a book. Details status selector. |
| Finished | Status filter segment. Status value on a book. Details status selector. |
| Status: All | Filter button at 200 % font scale. The second word is the current value. |
| 12 books | Count line with a full library in the mockups. Format the real count the same way. |
| 2 books match | Count line for a saved-book search that has matches. |
| Title A–Z | Sort button label and sort menu item (current order in the mockups). |
| Title Z–A | Sort menu item. |
| Author A–Z | Sort menu item. |
| Author Z–A | Sort menu item. |
| Sort by | Sort menu heading. |
| Search checks titles and authors of saved books on this device. Accents do not matter. | Note under saved-book search results. |
| No books yet | Empty Library title. |
| Find a book on Open Library or enter the details yourself. Your library stays on this device. | Empty Library body. |
| Restore backup | Empty Library text button. Also a section title on Backup and restore. |
| Middlemarch is saved in your library. | Snackbar after a successful save. Replace the title with the saved book title. |
| Open | Snackbar action that opens the saved book. |

## Find online

| String | Where it is used |
| --- | --- |
| Title | Field label on Find online, and on Review and save. |
| For example, Middlemarch | Title field placeholder. |
| Clear title | TalkBack label for the clear icon in the online title field. |
| Search Open Library | Primary button on Find online. |
| 4 works from Open Library | Results count line. Format the real count the same way. |
| Page 1 | Results page indicator. |
| First published {year} | Caption under a provider work, for example "George Eliot · First published 1871". |
| {n} editions | Caption under a provider work, for example "214 editions". |
| Results are not saved books. Choose an edition and then Review and save. | Notice under search results. |
| Choose an edition | Editions list heading. |
| 1–20 | Editions page range in the mockups. Format the real range the same way. This string uses an en dash. |
| Previous page | Editions paging button. Disabled on the first page. |
| Next page | Editions paging button. |
| Reprint | Edition note in sample data. |
| Oxford World's Classics | Edition note in sample data. |
| not listed | ISBN caption when an edition has no ISBN. |
| No connection | Offline title. |
| Open Library search needs a network connection. Your saved books are still on this device. | Offline body. |
| Go to Library | Offline secondary button. |
| Try again | Offline primary button. |
| Searching Open Library | TalkBack label for the in-progress search (specified, not drawn as a full screen). |

## Preview and import

| String | Where it is used |
| --- | --- |
| Edition preview | Screen title. |
| No cover loaded | Caption under the preview cover placeholder. |
| Preview only. Nothing is saved. | Notice on the preview. |
| Publisher | Metadata label. Field label on Review and save. |
| Publication date | Metadata label. Field label on Review and save. |
| Language | Metadata label. |
| ISBN | Metadata label. |
| Edition | Metadata label. |
| Metadata from Open Library · Edition {id} | Preview source line, for example "Metadata from Open Library · Edition OL7353617M". |
| Preview cover | Secondary button on the preview. |
| Review and save | Primary button on the preview. Screen title of the import draft. |
| Review metadata from Open Library. You can change it before you save. | Lead on the import draft. |
| (required) | Suffix after required field labels. |
| Enter an author. | Error under an empty required Author field. |
| Author | Field label. |
| I have permission to save this cover on my device | Cover permission checkbox label. The box starts unchecked. |
| Cover images can have separate copyright restrictions. Leave the box unchecked to save without a cover. | Help under the permission checkbox. |
| Save book | Primary button on the import draft. |

## Book details

| String | Where it is used |
| --- | --- |
| Edit book | TalkBack label for the edit icon in the details app bar. |
| Saved cover | Caption when the book has a saved cover. |
| Description | Section label. |
| Imported from Open Library · Edition {id} | Source line on a saved imported book, for example "Imported from Open Library · Edition OL24280934M". |
| Edit | Overflow menu item on book details. |
| Delete book | Overflow menu item. Destructive button on the delete dialog. |
| Delete this book? | Delete dialog title. |
| Folium deletes {title} and its saved cover from this device. You cannot undo this. | Delete dialog body. Use the book title. If the book has no saved cover, drop "and its saved cover". |
| Keep book | Safe action on the delete dialog. It comes first. |

## Backup and restore

| String | Where it is used |
| --- | --- |
| Export backup | Section title and primary button. |
| Write your {n} saved books to a file you choose. The file holds reading status, metadata and saved covers. | Export section body. Use the real count. |
| Replace the books on this device with the books in a backup file. You see the titles before anything changes. | Restore section body. |
| Choose backup file | Restore secondary button. |
| Folium does not upload backups. The file contains your reading history, so keep it in a private place. Folium reads version 1 backups up to 32 MiB. | Privacy notice. |
| Replace your library? | Restore confirmation notice title. |
| The {current} books on this device change to the {backup} books in this backup. Edits made after this backup are lost. | Restore confirmation notice body. Use the real counts. |
| {file name} · {n} books | Backup file summary, for example "Folium backup.json · 9 books". |
| And {n} more | Trailing line when the title list is shortened. |
| Replace library | Destructive primary button. |
| Keep current library | Safe secondary button. |

## TalkBack examples (not always visible)

| String | Where it is used |
| --- | --- |
| {title}, {author}, {status} | Book row, for example "Moby-Dick, Herman Melville, Reading". The cover is hidden from TalkBack. |
| selected | Spoken with the selected status segment. |
