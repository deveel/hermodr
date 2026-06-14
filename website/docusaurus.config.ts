import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'Hermodr',
  tagline: 'A lightweight, extensible CloudEvents framework for .NET',
  favicon: 'img/favicon.ico',

  future: {
    v4: true,
  },

  url: 'https://hermodr.deveel.org',
  baseUrl: '/',

  organizationName: 'deveel',
  projectName: 'hermodr',

  onBrokenLinks: 'warn',

  headTags: [
    {
      tagName: 'meta',
      attributes: {
        name: 'algolia-site-verification',
        content: '7E0BBB65DCBBD694',
      },
    },
    {
      tagName: 'link',
      attributes: {
        rel: 'apple-touch-icon',
        sizes: '180x180',
        href: '/img/apple-touch-icon.png',
      },
    },
    {
      tagName: 'link',
      attributes: {
        rel: 'icon',
        type: 'image/png',
        sizes: '32x32',
        href: '/img/icon-32.png',
      },
    },
    {
      tagName: 'link',
      attributes: {
        rel: 'icon',
        type: 'image/png',
        sizes: '192x192',
        href: '/img/icon-192.png',
      },
    },
    {
      tagName: 'link',
      attributes: {
        rel: 'icon',
        type: 'image/png',
        sizes: '512x512',
        href: '/img/icon-512.png',
      },
    },
  ],

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          path: '../docs',
          sidebarPath: './sidebars.ts',
          editUrl: 'https://github.com/deveel/hermodr/edit/main/docs/',
          versions: {
            current: {
              label: 'Next',
            },
          },
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/hermodr-full-logo.png',
    algolia: {
      appId: 'JYSR40O1I0',
      apiKey: '479261bc4e1a490dec72ff2d553c229b',
      indexName: 'hermodr',
      contextualSearch: false,
    },
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    navbar: {
      logo: {
        alt: 'Hermodr Logo',
        src: 'img/hermodr-full-logo.png',
        width: 106,
        height: 45,
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docs',
          position: 'left',
          label: 'Docs',
        },
        {
          type: 'docsVersionDropdown',
          position: 'right',
          dropdownItemsAfter: [
            {
              type: 'html',
              value: '<span class="header-right-group"><a class="header-github-link" href="https://github.com/deveel/hermodr" aria-label="GitHub repository" target="_blank" rel="noopener noreferrer"></a><a class="header-deveel-link" href="https://deveel.org" aria-label="Deveel website" target="_blank" rel="noopener noreferrer"><img src="/img/deveel-logo.svg" alt="Deveel" class="header-deveel-logo" /></a></span>',
            },
          ],
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            {
              label: 'Getting Started',
              to: '/docs/getting-started/installation',
            },
            {
              label: 'Core Concepts',
              to: '/docs/concepts/',
            },
            {
              label: 'Roadmap',
              to: '/docs/roadmap',
            },
          ],
        },
        {
          title: 'Packages',
          items: [
            {
              label: 'NuGet',
              href: 'https://www.nuget.org/profiles/hermodr',
            },
            {
              label: 'GitHub Packages',
              href: 'https://github.com/deveel/hermodr/packages',
            },
          ],
        },
        {
          title: 'Community',
          items: [
            {
              label: 'GitHub',
              href: 'https://github.com/deveel/hermodr',
            },
            {
              label: 'Issues',
              href: 'https://github.com/deveel/hermodr/issues',
            },
            {
              label: 'Deveel',
              href: 'https://deveel.org',
            },
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} Antonello Provenzano.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp'],
    },
  } satisfies Preset.ThemeConfig,

  plugins: [
    [
      '@docusaurus/plugin-client-redirects',
      {
        redirects: [],
      },
    ],
  ],
};

export default config;
