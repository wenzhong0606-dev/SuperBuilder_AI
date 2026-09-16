-- PMIS 字典绑定推断结果（自动生成，请人工复核后执行）
-- 字典源 DataSourceId=2 (PMIS)，业务源 DataSourceId=1 (WMS)，TenantId=4
-- 判定算法：贪心集合覆盖 —— 每轮挑选能覆盖最多「尚未覆盖码值」的 dict_type，直到增益 < max(1, 5%×distinct)。
--   不用单分类阈值法：单列码值跨多个 dict_type（实测 wms_storage_receipt.type 跨 4 类），阈值法会漏。
-- 反例：wms_storage_receipt.type 单分类最高仅 warehousing_type 60%；贪心并集后覆盖 10/10 = 100%。

-- ① 字典表配置（唯一约束 TenantId+DataSourceId+TableName）
INSERT INTO MetadataDictionaryConfigs
    (TenantId, DataSourceId, TableName, CodeColumn, NameColumn, TypeColumn,
     ActiveFilterColumn, ActiveFilterValue, IsEnabled, CreatedTime, RowVersion)
SELECT 4, 2, N'js_sys_dict_data', N'dict_code', N'dict_label', N'dict_type',
       N'status', N'0', 1, SYSUTCDATETIME(), 1
WHERE NOT EXISTS (SELECT 1 FROM MetadataDictionaryConfigs
                  WHERE TenantId = 4 AND DataSourceId = 2 AND TableName = N'js_sys_dict_data');

-- ② 可自动绑定列 10 个；未绑定列 24 个

-- wms_check_order.type (bigint) distinct=3 覆盖=3/3 (100%) → wms_check_type(+3)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'wms_check_type'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_check_order' AND c.ColumnName = N'type'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_check_order_info.unit_id (bigint) distinct=73 覆盖=73/73 (100%) → measure_unit(+73)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'measure_unit'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_check_order_info' AND c.ColumnName = N'unit_id'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_check_order_scan_info.unit_id (bigint) distinct=66 覆盖=66/66 (100%) → measure_unit(+66)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'measure_unit'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_check_order_scan_info' AND c.ColumnName = N'unit_id'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_delivery_receipt.type (bigint) distinct=12 覆盖=11/12 (92%) → outbound_type(+7) wms_check_type(+3) warehousing_type(+1)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'outbound_type,wms_check_type,warehousing_type'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_delivery_receipt' AND c.ColumnName = N'type'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_inventory.unit (bigint) distinct=126 覆盖=123/126 (98%) → measure_unit(+123)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'measure_unit'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_inventory' AND c.ColumnName = N'unit'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_inventory_record.type (bigint) distinct=14 覆盖=14/14 (100%) → variation_type(+14)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'variation_type'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_inventory_record' AND c.ColumnName = N'type'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_logistics_detail.unit_id (bigint) distinct=4 覆盖=4/4 (100%) → measure_unit(+4)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'measure_unit'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_logistics_detail' AND c.ColumnName = N'unit_id'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_storage_receipt.type (bigint) distinct=10 覆盖=10/10 (100%) → warehousing_type(+6) wms_check_type(+2) Input_output_type(+1) variation_type(+1)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'warehousing_type,wms_check_type,Input_output_type,variation_type'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_storage_receipt' AND c.ColumnName = N'type'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_storage_receipt_info.unit (bigint) distinct=126 覆盖=123/126 (98%) → measure_unit(+123)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'measure_unit'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_storage_receipt_info' AND c.ColumnName = N'unit'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- wms_transfer_slip_info.unit (bigint) distinct=44 覆盖=42/44 (95%) → measure_unit(+42)
UPDATE c SET c.IsDictBacked = 1, c.DictConfigId = d.Id, c.DictCategoryValue = N'measure_unit'
FROM MetadataColumns c
JOIN MetadataTables t ON t.Id = c.MetadataTableId
CROSS JOIN MetadataDictionaryConfigs d
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_transfer_slip_info' AND c.ColumnName = N'unit'
  AND d.TenantId = 4 AND d.DataSourceId = 2 AND d.TableName = N'js_sys_dict_data';

-- ③ 未绑定：字典表无归属，需其它策略
--    a) 小整数枚举（tinyint/int）：语义在列注释图例里，应走 ValueMapJson（列注释解析），非字典表
--    b) 确实不在字典表：需人工/学习指定
--   wms_check_order.status (tinyint, 注释枚举) distinct=5 样本=0,1,2,3,4
--   wms_check_order.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_check_order_info.unit (varchar, 需人工) distinct=70 样本=Pcs,m²,套,片,片(0222*0334),片(0819*0800),片(1002*1246),片(1242*0925)
--   wms_check_order_info.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_check_order_scan_info.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_delivery_receipt.status (tinyint, 注释枚举) distinct=5 样本=0,1,2,4,5
--   wms_delivery_receipt.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_delivery_receipt_info.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_inventory.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_logistics_detail.unpack_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_logistics_detail.unit_pack_quantity (int, 注释枚举) distinct=3 样本=0,1,20
--   wms_logistics_info.type (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_logistics_order.status (tinyint, 注释枚举) distinct=2 样本=1,2
--   wms_sign_feedback.state (int, 注释枚举) distinct=3 样本=0,1,2
--   wms_sign_feedback.signature_type (varchar, 需人工) distinct=3 样本=1,正常签收,签收异常
--   wms_sign_feedback.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_storage_receipt.status (tinyint, 注释枚举) distinct=6 样本=0,1,2,3,4,5
--   wms_storage_receipt.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_storage_receipt_info.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_test_inventory.type (int, 注释枚举) distinct=8 样本=0,1,2,3,4,5,6,9
--   wms_transfer_slip.type (bigint, 需人工) distinct=3 样本=1600404685319471104,1605088362526994432,1605088362526994433
--   wms_transfer_slip.status (tinyint, 注释枚举) distinct=4 样本=0,1,2,4
--   wms_transfer_slip.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
--   wms_transfer_slip_info.del_flag (tinyint, 注释枚举) distinct=2 样本=0,1
