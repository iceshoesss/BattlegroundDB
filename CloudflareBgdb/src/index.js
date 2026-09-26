/**
 * Cloudflare Worker - BattlegroundDB 下载服务
 *
 * 端点：
 *   GET /           - 版本信息
 *   GET /version    - 仅返回版本号与下载入口
 *   GET /download   - 下载 DLL 文件
 *   GET /cards.json - 下载完整卡牌 JSON（bg_cards.json）
 */

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    const path = url.pathname;

    const corsHeaders = {
      'Access-Control-Allow-Origin': '*',
      'Access-Control-Allow-Methods': 'GET, OPTIONS',
      'Access-Control-Allow-Headers': 'If-None-Match, If-Modified-Since',
    };

    if (request.method === 'OPTIONS') {
      return new Response(null, { headers: corsHeaders });
    }

    try {
      if (path === '/' || path === '/version') {
        return handleVersion(env, corsHeaders);
      }

      if (path === '/download') {
        return handleDownload(request, env, corsHeaders);
      }

      if (path === '/cards.json' || path === '/json' || path === '/bg_cards.json') {
        return handleCardsJson(request, env, corsHeaders);
      }

      return new Response('Not Found', { status: 404, headers: corsHeaders });
    } catch (error) {
      return new Response(`Error: ${error.message}`, {
        status: 500,
        headers: corsHeaders,
      });
    }
  },
};

async function handleVersion(env, headers) {
  const version = env.BGDB_VERSION || '2.0.0';
  const filename = env.BGDB_FILENAME || 'BattlegroundDB.dll';
  const updatedAt = (await env.BGDB_KV?.get('updatedAt')) || null;

  // JSON 版本可能与 DLL 版本一致；无 KV 时回落到 env
  const jsonMeta = await readJsonMeta(env);

  const response = {
    version: version,
    filename: filename,
    downloadUrl: `/download`,
    jsonUrl: `/cards.json`,
    jsonVersion: jsonMeta.version,
    updatedAt: updatedAt,
  };

  return new Response(JSON.stringify(response, null, 2), {
    headers: {
      ...headers,
      'Content-Type': 'application/json',
      'Cache-Control': 'public, max-age=3600',
    },
  });
}

/**
 * 读取 JSON 元信息（优先 json/latest）
 */
async function readJsonMeta(env) {
  const raw = await env.BGDB_KV?.get('json/latest');
  if (!raw) return { version: env.BGDB_VERSION || '2.0.0', totalCards: null };
  try {
    const data = JSON.parse(raw);
    return {
      version: data?.meta?.version || env.BGDB_VERSION || '2.0.0',
      totalCards: data?.meta?.totalCards ?? (data?.cards?.length ?? null),
    };
  } catch {
    return { version: env.BGDB_VERSION || '2.0.0', totalCards: null };
  }
}

async function handleDownload(request, env, headers) {
  const version = env.BGDB_VERSION || '2.0.0';
  const filename = env.BGDB_FILENAME || 'BattlegroundDB.dll';

  const dllKey = `dll/${version}`;
  const dll = await env.BGDB_KV?.get(dllKey, { type: 'arrayBuffer' });

  if (!dll) {
    return new Response('DLL not found. Please upload the DLL to KV first.', {
      status: 404,
      headers: {
        ...headers,
        'Content-Type': 'text/plain',
      },
    });
  }

  const etag = `"${version}"`;
  const ifNoneMatch = request.headers.get('If-None-Match');
  if (ifNoneMatch === etag) {
    return new Response(null, {
      status: 304,
      headers: {
        ...headers,
        ETag: etag,
      },
    });
  }

  return new Response(dll, {
    headers: {
      ...headers,
      'Content-Type': 'application/octet-stream',
      'Content-Disposition': `attachment; filename="${filename}"`,
      'Content-Length': dll.byteLength.toString(),
      ETag: etag,
      'Cache-Control': 'public, max-age=86400',
    },
  });
}

/**
 * 完整卡牌 JSON（bg_cards.json）
 * 数据来自 KV：json/latest（CI 上传）
 */
async function handleCardsJson(request, env, headers) {
  const url = new URL(request.url);
  const wantVersion = url.searchParams.get('version');
  const key = wantVersion ? `json/${wantVersion}` : 'json/latest';

  const body = await env.BGDB_KV?.get(key);
  if (!body) {
    return new Response(
      wantVersion
        ? `JSON not found for version ${wantVersion}`
        : 'JSON not found. Please upload bg_cards.json to KV first.',
      {
        status: 404,
        headers: {
          ...headers,
          'Content-Type': 'text/plain',
        },
      },
    );
  }

  // 尽量从内容里取版本号做 ETag
  let version = env.BGDB_VERSION || '0';
  try {
    const data = JSON.parse(body);
    version = data?.meta?.version || version;
  } catch {
    /* ignore */
  }

  const etag = `"${version}"`;
  const ifNoneMatch = request.headers.get('If-None-Match');
  if (ifNoneMatch === etag) {
    return new Response(null, {
      status: 304,
      headers: {
        ...headers,
        ETag: etag,
      },
    });
  }

  return new Response(body, {
    headers: {
      ...headers,
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Disposition': 'inline; filename="bg_cards.json"',
      ETag: etag,
      'Cache-Control': 'public, max-age=3600',
    },
  });
}
