/**
 * Help, organised by task. Each topic names the pages it belongs to, so help opens on what is in
 * front of the person, and the page someone must be able to open to need it, so Staff are not
 * shown the forecast or Team & access.
 */

export type HelpBlock =
  | { kind: 'text'; text: string }
  | { kind: 'steps'; items: string[] }
  | { kind: 'list'; items: string[] }
  | { kind: 'terms'; items: [string, string][] }
  | { kind: 'keys'; items: [string[], string][] };

export interface HelpTopic {
  id: string;
  title: string;
  summary: string;
  pages: string[];
  /** The page this needs access to; topics for pages a role cannot open are left out. */
  access?: string;
  keywords: string;
  blocks: HelpBlock[];
  related?: string[];
}

export const helpTopics: HelpTopic[] = [
  {
    id: 'record-booking',
    title: 'Record a booking',
    summary: 'Add a booking for a package or a tailored trip.',
    pages: ['/bookings', '/home', '/packages'],
    access: '/bookings',
    keywords: 'new booking add client passengers price package tailored custom quotation seats slots unusual amount',
    blocks: [
      {
        kind: 'steps',
        items: [
          'On Bookings, choose Add booking (or press N).',
          'Pick Ready-made package, or Tailored trip for a quotation of your own.',
          'Choose the package. Each one shows the seats left; a full package cannot be picked.',
          'Enter the client and the number of passengers. The price fills in as passengers × the package price; change it for a discount or extras.',
          'Set the payment and booking status, then Save booking.',
        ],
      },
      {
        kind: 'text',
        text: 'The booking date is the day the booking was made. The forecast counts bookings by this date, so it should not be the travel date.',
      },
      {
        kind: 'text',
        text: 'A price far above the usual booking is queried beside the price before it is saved. One mistyped figure would read as a seasonal spike in the forecast for a year.',
      },
    ],
    related: ['payments', 'void-restore', 'packages'],
  },
  {
    id: 'payments',
    title: 'Mark bookings as paid or confirmed',
    summary: 'Update one booking from its panel, or several at once.',
    pages: ['/bookings', '/home'],
    access: '/bookings',
    keywords: 'paid payment confirm confirmed status bulk several select many undo partially paid booked value unpaid',
    blocks: [
      {
        kind: 'steps',
        items: [
          'Open the booking from the list, then choose Mark as paid or Confirm booking.',
          'For several at once, tick their boxes (shift-click ticks a run of rows; on a phone, tap Select first), then choose Mark as paid or Confirm in the bar at the bottom.',
          'Changed your mind? Choose Undo on the notice, or press Ctrl+Z (⌘Z on a Mac) while it shows.',
        ],
      },
      {
        kind: 'text',
        text: 'Partially paid is a status only: the amounts of part-payments are not recorded. That is why the unpaid figure is called booked value, the value of the bookings, not the balance owed.',
      },
    ],
    related: ['void-restore', 'shortcuts'],
  },
  {
    id: 'void-restore',
    title: 'Void or restore a record',
    summary: 'Take a wrong or cancelled record out of the totals without losing it.',
    pages: ['/bookings', '/expenses'],
    access: '/bookings',
    keywords: 'void delete remove cancel cancelled duplicate mistake restore undo voided history',
    blocks: [
      {
        kind: 'text',
        text: 'Records are voided rather than deleted. A voided record keeps its history but is left out of every total, Reports and the forecast. A voided booking gives its seats back to the package.',
      },
      {
        kind: 'steps',
        items: [
          'Open the record, choose Void, and say why (for example, entered twice or client cancelled).',
          'To see voided records, turn on Show voided above the list.',
          'Open a voided record and choose Restore to count it again. A booking can only be restored while its package has the seats.',
        ],
      },
    ],
    related: ['payments'],
  },
  {
    id: 'packages',
    title: 'Keep packages and seats up to date',
    summary: 'What open, sold and reserved slots mean, and how bookings change them.',
    pages: ['/packages'],
    access: '/packages',
    keywords: 'package slots seats open sold reserved low critical status photo price catalog capacity',
    blocks: [
      {
        kind: 'terms',
        items: [
          ['Open slots', 'Seats still for sale. A booking moves its passengers from open to sold.'],
          ['Sold slots', 'Seats taken by bookings, kept up to date as bookings are recorded, edited or voided.'],
          ['Reserved slots', 'Seats held without a booking, such as an unconfirmed group.'],
          ['Low and Critical', 'Statuses you set. They put the package on Home’s attention list and under Low on slots.'],
        ],
      },
      {
        kind: 'text',
        text: 'Open a package to see its seats, photo and history, to book it straight away, or to see its bookings.',
      },
    ],
    related: ['record-booking', 'forecast-decisions'],
  },
  {
    id: 'expenses',
    title: 'Record and settle expenses',
    summary: 'Add costs, keep track of what is still owed, and mark it paid.',
    pages: ['/expenses'],
    access: '/expenses',
    keywords: 'expense cost spending add pending owed paid category related package supplier receipt',
    blocks: [
      {
        kind: 'steps',
        items: [
          'Choose Add expense (or press N), then the date, category and amount.',
          'Pending means still owed. Mark it paid from its panel when it is settled, or tick several and mark them paid together.',
          'Name the related package to keep its costs together in Reports.',
        ],
      },
      {
        kind: 'text',
        text: 'Expenses are recorded, not forecast. The spending suggestions above the list come from the revenue forecast and point to last year’s spending in the same month.',
      },
    ],
    related: ['forecast-decisions', 'export'],
  },
  {
    id: 'read-forecast',
    title: 'Read the forecast',
    summary: 'What the line, the shaded range and the average miss mean.',
    pages: ['/home', '/bookings', '/packages', '/expenses'],
    access: '/home',
    keywords: 'forecast holt-winters holt winters model prediction range likely 80% average miss mape season trend 24 months chart line seasonal accuracy',
    blocks: [
      {
        kind: 'text',
        text: 'The forecast uses Holt-Winters, a method that learns three things from past months: the usual level, the trend, and the pattern that repeats every 12 months. It projects them six months ahead.',
      },
      {
        kind: 'terms',
        items: [
          ['Solid navy line', 'Recorded months: gross booking revenue (or bookings, or passengers) without voided bookings.'],
          ['Dashed teal line', 'The forecast. Teal always marks something the model produced, never a recorded figure.'],
          ['Shaded range', 'The likely range: in about 8 months out of 10, the real figure should land inside it.'],
          ['Average miss', 'How far the model’s fitted values were from the recorded months, on average. It describes the fit on the past, not a promise about the future.'],
        ],
      },
      {
        kind: 'list',
        items: [
          'It needs at least 24 months of records to tell the seasonal pattern from the trend.',
          'If recent months have no bookings, the forecast works from the last month that has them, and says so. Record those months to bring it up to date.',
          'Counts are by booking date, for the whole agency.',
          'It does not forecast expenses, cash, profit, or individual packages.',
        ],
      },
    ],
    related: ['forecast-decisions', 'glossary'],
  },
  {
    id: 'forecast-decisions',
    title: 'Act on the forecast’s suggestions',
    summary: 'Each suggestion shows its evidence and a next step. The decision stays yours.',
    pages: ['/home', '/expenses', '/packages', '/bookings'],
    access: '/home',
    keywords: 'decision decisions suggestion recommendation review evidence peak uncertainty decrease spending plan',
    blocks: [
      {
        kind: 'steps',
        items: [
          'Read the suggestion and the evidence under it: the months it rests on and how they compare.',
          'Select it to see those months on the chart.',
          'Follow its link, usually to the same month last year, to check what actually happened then.',
          'Weigh it against what you know that the model cannot: bookings in hand, supplier quotes, events.',
        ],
      },
      {
        kind: 'text',
        text: 'Revenue suggestions are about spending. Booking and passenger suggestions are about preparing for demand: seats, staff and suppliers.',
      },
    ],
    related: ['read-forecast'],
  },
  {
    id: 'reports',
    title: 'Review a period in Reports',
    summary: 'Totals for any period, compared like for like with the one before.',
    pages: ['/reports'],
    access: '/reports',
    keywords: 'report reports period month year compare comparison previous revenue costs after costs profit print export csv breakdown',
    blocks: [
      {
        kind: 'steps',
        items: [
          'Choose a period. It is kept in the address, so the page can be bookmarked or shared.',
          'Read the statement: this period, the one before it, and the change. A period that is not over yet is compared with the same stretch of the one before.',
          'Print it, or Export CSV for the monthly figures and a total.',
        ],
      },
      {
        kind: 'terms',
        items: [
          ['Revenue', 'Gross booking revenue, without voided bookings: the same figure on every page.'],
          ['Costs', 'Recorded expenses, without voided ones.'],
          ['Revenue after costs', 'Revenue less recorded costs. It is not accounting profit.'],
        ],
      },
    ],
    related: ['export', 'glossary'],
  },
  {
    id: 'import',
    title: 'Import from a spreadsheet',
    summary: 'Bring bookings or packages in from a CSV file, with a preview first.',
    pages: ['/bookings', '/packages'],
    access: '/bookings',
    keywords: 'import csv spreadsheet excel sheet upload file history bulk add',
    blocks: [
      {
        kind: 'steps',
        items: [
          'Save the sheet as a CSV file.',
          'Choose More, then Import from a file, and pick the file.',
          'Check the preview: what will be imported, what is already in the system, and any rows that cannot be read. Nothing is saved yet.',
          'Choose Import to add them.',
        ],
      },
      {
        kind: 'list',
        items: [
          'Bookings need date, client, package, destination, passengers and revenue. Imported bookings count as history and do not take seats from packages on sale.',
          'Packages need package, destination and price.',
        ],
      },
    ],
    related: ['export'],
  },
  {
    id: 'export',
    title: 'Export for the accountant',
    summary: 'Download records as CSV files that open in a spreadsheet.',
    pages: ['/bookings', '/expenses', '/reports', '/packages'],
    access: '/bookings',
    keywords: 'export download csv spreadsheet excel accountant backup file',
    blocks: [
      {
        kind: 'list',
        items: [
          'Bookings: More, then Export bookings. Every booking, with voided ones marked.',
          'Packages: More, then Export packages.',
          'Expenses and Reports, for owners and admins: Export CSV, for the period chosen on the page.',
        ],
      },
    ],
  },
  {
    id: 'team',
    title: 'Give someone access',
    summary: 'Add accounts, choose what each person can open, and suspend access.',
    pages: ['/team'],
    access: '/team',
    keywords: 'team account user staff admin owner access level role password suspend reactivate sign in login',
    blocks: [
      {
        kind: 'terms',
        items: [
          ['Owner / Management', 'Everything, including Team & access.'],
          ['Admin', 'Everything except Team & access.'],
          ['Staff', 'Bookings and packages only. Staff never see the forecast.'],
        ],
      },
      {
        kind: 'list',
        items: [
          'New accounts start as Staff, with a temporary password you share privately.',
          'Changing someone’s access, suspending them or setting a new password signs them out straight away.',
          'Nobody can change their own access or suspend themselves, and there is always one active owner.',
        ],
      },
    ],
  },
  {
    id: 'search',
    title: 'Find anything quickly',
    summary: 'Jump to any page, action or record from one search.',
    pages: [],
    keywords: 'search find jump go command palette ctrl k recent quick open',
    blocks: [
      {
        kind: 'steps',
        items: [
          'Press Ctrl+K (⌘K on a Mac), or choose Search.',
          'Type part of a client, package, expense, ID or page name, then press Enter.',
          'With nothing typed, it lists what you opened recently.',
        ],
      },
      {
        kind: 'text',
        text: 'Filters on every list are kept in the address, so a filtered list can be bookmarked or sent to someone.',
      },
    ],
    related: ['shortcuts'],
  },
  {
    id: 'shortcuts',
    title: 'Keyboard shortcuts',
    summary: 'Keys for the things done most often.',
    pages: [],
    keywords: 'keyboard shortcuts keys hotkeys fast quick',
    blocks: [
      {
        kind: 'keys',
        items: [
          [['Ctrl', 'K'], 'Search and jump anywhere'],
          [['/'], 'Search the list on this page'],
          [['N'], 'Add a record on this page'],
          [['E'], 'Edit the record that is open'],
          [['G', 'then', 'H'], 'Go to Home (B Bookings, P Packages, E Expenses, R Reports, T Team)'],
          [['Ctrl', 'Z'], 'Undo the last change while its notice shows'],
          [['Shift', 'click'], 'Tick a run of rows'],
          [['Esc'], 'Close a panel, menu or search'],
          [['?'], 'Open this help'],
        ],
      },
      {
        kind: 'text',
        text: 'On a Mac, use ⌘ where Ctrl is shown. Shortcuts wait while you are typing in a field.',
      },
    ],
  },
  {
    id: 'glossary',
    title: 'Words used here',
    summary: 'What the figures and labels mean.',
    pages: [],
    keywords: 'glossary meaning definition terms booked value revenue after costs likely range average miss voided pending reserved',
    blocks: [
      {
        kind: 'terms',
        items: [
          ['Revenue', 'Gross booking revenue, without voided bookings.'],
          ['Booked value', 'The value of bookings not yet fully paid. Part-payments are not recorded, so it is not the balance owed.'],
          ['Revenue after costs', 'Revenue less recorded expenses. Not accounting profit.'],
          ['By booking date', 'Counted in the month the booking was made, not the month of travel.'],
          ['Likely range', 'The forecast’s 80% range: the real figure should land inside it in about 8 months out of 10.'],
          ['Average miss', 'How far the forecast’s fitted values were from recorded months, on average.'],
          ['Voided', 'Kept with its history, but left out of every total and the forecast.'],
          ['Pending', 'For a payment, nothing paid yet; for an expense, still owed; for a booking, not yet reserved or confirmed.'],
        ],
      },
    ],
  },
];

export function topicById(id: string): HelpTopic | undefined {
  return helpTopics.find((topic) => topic.id === id);
}

/** The text a topic is searched on: its title, summary, words people might use, and its body. */
export function topicText(topic: HelpTopic): string {
  const body = topic.blocks
    .map((block) => {
      switch (block.kind) {
        case 'text':
          return block.text;
        case 'steps':
        case 'list':
          return block.items.join(' ');
        case 'terms':
          return block.items.map(([term, meaning]) => `${term} ${meaning}`).join(' ');
        case 'keys':
          return block.items.map(([keys, meaning]) => `${keys.join(' ')} ${meaning}`).join(' ');
      }
    })
    .join(' ');
  return `${topic.title} ${topic.summary} ${topic.keywords} ${body}`.toLowerCase();
}
