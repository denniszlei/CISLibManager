-- ==============================================================================
-- Cadence OrCAD CIS (Component Information System) MariaDB / MySQL 初始化脚本
-- 兼容：Cadence 17.4 / 22.1 / 23.1 + MariaDB 10.5+ / MySQL 8.0+
-- 字符集：utf8mb4 (支持各种特殊技术符号，如 Ω, µ, ±, ℃, ⌀)
-- ==============================================================================

CREATE DATABASE IF NOT EXISTS `cadence_cis_db` 
    DEFAULT CHARACTER SET utf8mb4 
    DEFAULT COLLATE utf8mb4_unicode_ci;

USE `cadence_cis_db`;

-- 禁用外键检查（方便重建）
SET FOREIGN_KEY_CHECKS = 0;

-- ------------------------------------------------------------------------------
-- 1. 电阻库 (Resistors)
-- 标准件模式：Schematic_Part 指向 "DISCRETE\RES"，PCB_Footprint 指向标准封装 "R0402" 等
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_resistors`;
CREATE TABLE `cis_resistors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '系统自增主键',
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '公司自建物料号 (如 RES-0402-10K-00001)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称值 (如 10k, 4.7R, 100k)',
    `Tolerance` VARCHAR(32) DEFAULT '±1%' COMMENT '阻值精度 (如 ±1%, ±5%, ±0.1%)',
    `Power_Rating` VARCHAR(32) DEFAULT '1/16W' COMMENT '额定功率 (如 1/16W, 0.1W, 0.25W)',
    `Package` VARCHAR(64) NOT NULL COMMENT '公制/英制封装 (如 0402, 0603, 0805, 1206, 2512)',
    `Description` VARCHAR(255) DEFAULT NULL COMMENT '详细物料描述 (由DigiKey/立创抓取或自动生成)',
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\RES' COMMENT 'OrCAD原理图符号 (<库名>\\<符号名>)',
    `PCB_Footprint` VARCHAR(128) NOT NULL COMMENT 'Allegro PCB封装名称 (如 R0402, R0603, R0805)',
    `Model_3D` VARCHAR(128) DEFAULT NULL COMMENT 'STEP 3D模型文件名 (如 R0402.step)',
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'R' COMMENT '原理图默认位号前缀',
    `Manufacturer` VARCHAR(128) DEFAULT NULL COMMENT '生产原厂 (如 Yageo, UniOhm, Walsin, Panasonic)',
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL COMMENT '原厂完整订货型号 (MPN)',
    `Datasheet` VARCHAR(512) DEFAULT NULL COMMENT '规格书链接或文件路径',
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active' COMMENT '生命周期状态 (Active / NRND / Obsolete)',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL COMMENT '立创商城物料编号 (如 C25744)',
    `Price_RMB` VARCHAR(32) DEFAULT NULL COMMENT '打样/批量参考单价 (元)',
    `Temp_Coefficient` VARCHAR(32) DEFAULT '±100ppm/℃' COMMENT '温度系数 (TCR)',
    `Operating_Temperature` VARCHAR(64) DEFAULT '-55℃ ~ +155℃' COMMENT '工作温度范围',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`),
    INDEX `idx_status` (`Lifecycle_Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电阻物料库 (标准件/通用件)';

-- ------------------------------------------------------------------------------
-- 2. 电容库 (Capacitors)
-- 标准件模式：Schematic_Part 指向 "DISCRETE\CAP"，贴片无极性电容复用标准封装
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_capacitors`;
CREATE TABLE `cis_capacitors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '公司自建物料号 (如 CAP-0402-100NF-00001)',
    `Value` VARCHAR(64) NOT NULL COMMENT '标称容值 (如 100nF, 10uF, 22pF)',
    `Tolerance` VARCHAR(32) DEFAULT '±10%' COMMENT '容值公差 (如 ±5%, ±10%, ±20%)',
    `Voltage_Rating` VARCHAR(32) DEFAULT '16V' COMMENT '额定耐压 (如 6.3V, 16V, 25V, 50V, 100V)',
    `Dielectric` VARCHAR(32) DEFAULT 'X7R' COMMENT '介质材料/电介质 (如 NPO/C0G, X5R, X7R, 钽电容, 铝电解)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装代码 (如 0402, 0603, 0805, 1206, CASE-A/3216)',
    `Description` VARCHAR(255) DEFAULT NULL COMMENT '物料描述',
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\CAP' COMMENT '原理图符号 (<库名>\\<符号名>)',
    `PCB_Footprint` VARCHAR(128) NOT NULL COMMENT 'Allegro PCB封装名称 (如 C0402, C0603, CAP_ELEC_D6.3)',
    `Model_3D` VARCHAR(128) DEFAULT NULL COMMENT '3D模型文件名 (如 C0402.step)',
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'C' COMMENT '默认位号前缀',
    `Manufacturer` VARCHAR(128) DEFAULT NULL COMMENT '生产厂商 (如 Murata, TDK, Samsung, Taiyo Yuden)',
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL COMMENT '原厂型号',
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Operating_Temperature` VARCHAR(64) DEFAULT '-55℃ ~ +125℃',
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电容物料库';

-- ------------------------------------------------------------------------------
-- 3. 电感与磁珠 (Inductors & Beads)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_inductors`;
CREATE TABLE `cis_inductors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '公司物料号 (如 IND-0603-1UH-00001)',
    `Value` VARCHAR(64) NOT NULL COMMENT '感值/阻抗 (如 2.2uH, 10uH, 120R@100MHz)',
    `Tolerance` VARCHAR(32) DEFAULT '±20%' COMMENT '精度',
    `Rated_Current` VARCHAR(32) DEFAULT NULL COMMENT '额定电流 / 饱和电流 (如 1.5A, 3.2A)',
    `DCR` VARCHAR(32) DEFAULT NULL COMMENT '直流内阻 (如 45mΩ, 120mΩ)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 0603, 0805, 4020, 6045)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL DEFAULT 'DISCRETE\\IND' COMMENT '原理图符号',
    `PCB_Footprint` VARCHAR(128) NOT NULL COMMENT 'PCB封装',
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'L' COMMENT '位号前缀 (L / FB)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_val_pkg` (`Value`, `Package`),
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电感与磁珠物料库';

-- ------------------------------------------------------------------------------
-- 4. 二极管与整流/保护器件 (Diodes & TVS)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_diodes`;
CREATE TABLE `cis_diodes` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 DIO-SOD123-B5819W-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '类别 (肖特基 / 稳压二极管 / 开关管 / TVS / 发光二极管LED)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '关键参数 (如 40V/1A, 3.3V, RED, BLUE)',
    `Breakdown_Voltage` VARCHAR(32) DEFAULT NULL COMMENT '击穿/反向耐压 (Vr/Vbr)',
    `Forward_Current` VARCHAR(32) DEFAULT NULL COMMENT '正向额定电流 (If)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 SOD-123, SOD-323, SMA, SMB, 0603_LED)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL COMMENT '原理图符号 (如 DISCRETE\\DIODE, DISCRETE\\LED, DISCRETE\\TVS)',
    `PCB_Footprint` VARCHAR(128) NOT NULL COMMENT 'PCB封装',
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'D' COMMENT '位号前缀 (D / TVS / LED)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='二极管与保护器件库';

-- ------------------------------------------------------------------------------
-- 5. 晶体管与场效应管 (Transistors & MOSFETs)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_transistors`;
CREATE TABLE `cis_transistors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 MOS-SOT23-AO3400A-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '类型 (N-MOS / P-MOS / NPN / PNP / IGBT)',
    `Vds_Vceo` VARCHAR(32) DEFAULT NULL COMMENT '耐压 (Vds / Vceo，如 30V, 60V, 100V)',
    `Id_Ic` VARCHAR(32) DEFAULT NULL COMMENT '额定电流 (Id / Ic，如 5.8A, 10A)',
    `Rds_on` VARCHAR(32) DEFAULT NULL COMMENT '导通内阻 (如 28mΩ@4.5V)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 SOT-23, SOT-23-3, SOP-8, DFN3x3, TO-252)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL COMMENT '原理图符号 (如 DISCRETE\\NMOS, DISCRETE\\NPN)',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'Q',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='三极管与MOSFET库';

-- ------------------------------------------------------------------------------
-- 6. 电源管理芯片 (Power Management ICs)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_power_ics`;
CREATE TABLE `cis_power_ics` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 PMIC-SOT23-5-AMS1117-3.3-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '类型 (LDO / Buck DCDC / Boost DCDC / 电池充电IC / 理想二极管)',
    `Vin_Range` VARCHAR(32) DEFAULT NULL COMMENT '输入电压范围 (如 2.5V ~ 5.5V, 4.5V ~ 36V)',
    `Vout` VARCHAR(32) DEFAULT NULL COMMENT '输出电压 (如 3.3V, 5.0V, Adj)',
    `Iout_Max` VARCHAR(32) DEFAULT NULL COMMENT '最大输出电流 (如 500mA, 1A, 3A)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 SOT-23-5, SOT-89, ESOP-8, QFN-16_3x3)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL COMMENT '专用原理图符号 (<专用库名>\\<型号>)',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='电源管理芯片库';

-- ------------------------------------------------------------------------------
-- 7. 微处理器与核心芯片 (MCU / DSP / FPGA / SoC)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_mcus`;
CREATE TABLE `cis_mcus` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 MCU-LQFP48-STM32G030F4P6-00001)',
    `Core` VARCHAR(64) DEFAULT NULL COMMENT '内核架构 (如 Cortex-M0+, Cortex-M4, RISC-V, ESP32)',
    `Frequency` VARCHAR(32) DEFAULT NULL COMMENT '主频 (如 64MHz, 168MHz)',
    `Flash_Size` VARCHAR(32) DEFAULT NULL COMMENT '闪存容量 (如 32KB, 64KB, 512KB)',
    `RAM_Size` VARCHAR(32) DEFAULT NULL COMMENT '内存容量 (如 8KB, 64KB, 128KB)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 TSSOP-20, QFN-32, LQFP-48, LQFP-64, BGA-100)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL COMMENT '原理图符号 (<MCU库名>\\<芯片型号>)',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='微处理器与核心主控芯片库';

-- ------------------------------------------------------------------------------
-- 8. 模拟与接口芯片 (Analog & Interface ICs)
-- 包含：运放、比较器、ADC/DAC、RS485/CAN/232收发器、以太网PHY
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_analog_ics`;
CREATE TABLE `cis_analog_ics` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 IC-SOP8-TJA1050-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '器件子类 (运放 / 比较器 / RS485 / CAN收发器 / ADC / 音频功放)',
    `Channels` VARCHAR(16) DEFAULT NULL COMMENT '通道数 (如 1, 2, 4)',
    `Bandwidth_Speed` VARCHAR(32) DEFAULT NULL COMMENT '增益带宽积/波特率 (如 10MHz, 1Mbps, 5Mbps)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 SOT-23-5, SOP-8, MSOP-8, TSSOP-14)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='模拟与接口芯片库';

-- ------------------------------------------------------------------------------
-- 9. 逻辑器件与存储器 (Logic & Memory ICs)
-- 包含：74系列、电平转换、EEPROM (I2C/SPI)、NOR Flash
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_logic_memory`;
CREATE TABLE `cis_logic_memory` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 MEM-SOP8-W25Q128JVSIQ-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '类型 (74系列逻辑 / 双向电平转换 / SPI NOR Flash / I2C EEPROM)',
    `Capacity` VARCHAR(32) DEFAULT NULL COMMENT '存储容量/位数 (如 2Kb, 16Mb, 128Mb, 8-bit)',
    `Voltage_Range` VARCHAR(32) DEFAULT '2.7V ~ 3.6V' COMMENT '工作电压',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 SOP-8, USON-8, TSSOP-16)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'U',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='数字逻辑与存储器件库';

-- ------------------------------------------------------------------------------
-- 10. 连接器与接插件 (Connectors & Headers)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_connectors`;
CREATE TABLE `cis_connectors` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 CON-TYPE-C-16P-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '类型 (Type-C / Micro-USB / 排针 / 排母 / 简牛 / 接线端子 / RJ45 / FPC)',
    `Pins` INT NOT NULL COMMENT '引脚总数 (如 2, 4, 8, 16, 24)',
    `Pitch` VARCHAR(32) DEFAULT NULL COMMENT '引脚间距 (如 0.5mm, 1.25mm, 2.0mm, 2.54mm)',
    `Mount_Type` VARCHAR(32) DEFAULT 'SMD' COMMENT '安装方式 (SMD贴片 / DIP通孔 / 沉板 / 侧贴)',
    `Package` VARCHAR(64) DEFAULT NULL COMMENT '封装名或外形代号',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL COMMENT '原理图符号',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'J' COMMENT '位号前缀 (J / CN / P)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='连接器与端子物料库';

-- ------------------------------------------------------------------------------
-- 11. 晶体晶振与时钟 (Crystals & Oscillators)
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_crystals`;
CREATE TABLE `cis_crystals` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 XTAL-3215-32.768K-00001)',
    `Frequency` VARCHAR(32) NOT NULL COMMENT '标称频率 (如 32.768kHz, 8MHz, 16MHz, 24MHz, 25MHz)',
    `Frequency_Tolerance` VARCHAR(32) DEFAULT '±10ppm' COMMENT '频率公差/频偏',
    `Load_Capacitance` VARCHAR(32) DEFAULT '12.5pF' COMMENT '负载电容 (如 9pF, 12.5pF, 18pF)',
    `Type` VARCHAR(32) DEFAULT 'Passive' COMMENT '形式 (无源晶体 Passive / 有源晶振 Active / TCXO / VCXO)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装 (如 3215, 3225-4P, 2016, 5032)',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL COMMENT '原理图符号 (如 DISCRETE\\XTAL, DISCRETE\\OSC)',
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'Y' COMMENT '位号前缀 (Y / X)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='晶体与晶振时钟库';

-- ------------------------------------------------------------------------------
-- 12. 机电与杂项 (Electromechanical & Miscellaneous)
-- 包含：轻触按键、拨码开关、自恢复保险丝(PPTC)、蜂鸣器、继电器、射频天线、模块
-- ------------------------------------------------------------------------------
DROP TABLE IF EXISTS `cis_miscellaneous`;
CREATE TABLE `cis_miscellaneous` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
    `Part_Number` VARCHAR(64) NOT NULL UNIQUE COMMENT '物料号 (如 SW-SMD-3x4-00001, FUSE-1206-500MA-00001)',
    `Type` VARCHAR(64) NOT NULL COMMENT '器件类别 (按键开关 / 贴片保险丝 / 蜂鸣器 / 继电器 / 天线 / 模块)',
    `Value` VARCHAR(64) DEFAULT NULL COMMENT '标称值/规格 (如 0.5A/24V, 2400-2500MHz, 85dB)',
    `Package` VARCHAR(64) NOT NULL COMMENT '封装',
    `Description` VARCHAR(255) DEFAULT NULL,
    `Schematic_Part` VARCHAR(128) NOT NULL,
    `PCB_Footprint` VARCHAR(128) NOT NULL,
    `Model_3D` VARCHAR(128) DEFAULT NULL,
    `Part_Reference` VARCHAR(16) NOT NULL DEFAULT 'SW' COMMENT '位号前缀 (SW / F / BZ / K / ANT / M)',
    `Manufacturer` VARCHAR(128) DEFAULT NULL,
    `Manufacturer_Part_Number` VARCHAR(128) DEFAULT NULL,
    `Datasheet` VARCHAR(512) DEFAULT NULL,
    `Lifecycle_Status` VARCHAR(64) NOT NULL DEFAULT 'Active',
    `LCSC_Part_Number` VARCHAR(64) DEFAULT NULL,
    `Price_RMB` VARCHAR(32) DEFAULT NULL,
    `Created_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    `Updated_At` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_mpn` (`Manufacturer_Part_Number`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='机电与杂项器件库';

-- ------------------------------------------------------------------------------
-- 初始基础数据示例 (Seed Data)
-- 演示标准料如何共享 DISCRETE\RES 与 DISCRETE\CAP 符号及标准封装
-- ------------------------------------------------------------------------------
INSERT INTO `cis_resistors` 
(`Part_Number`, `Value`, `Tolerance`, `Power_Rating`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`) 
VALUES
('RES-0402-10K-00001', '10k', '±1%', '1/16W', '0402', 'RES SMD 10K OHM 1% 1/16W 0402', 'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo', 'RC0402FR-0710KL', 'Active', 'C25744'),
('RES-0402-4K7-00002', '4.7k', '±1%', '1/16W', '0402', 'RES SMD 4.7K OHM 1% 1/16W 0402', 'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo', 'RC0402FR-074K7L', 'Active', 'C25900'),
('RES-0402-0R-00003',  '0R',  '±5%', '1/16W', '0402', 'RES SMD 0 OHM JUMPER 1/16W 0402', 'DISCRETE\\RES', 'R0402', 'R0402.step', 'R', 'Yageo', 'RC0402JR-070RL', 'Active', 'C17168'),
('RES-0603-100R-00004','100R', '±1%', '1/10W', '0603', 'RES SMD 100 OHM 1% 1/10W 0603', 'DISCRETE\\RES', 'R0603', 'R0603.step', 'R', 'UniOhm', '0603WAJ0101T5E', 'Active', 'C22775');

INSERT INTO `cis_capacitors`
(`Part_Number`, `Value`, `Tolerance`, `Voltage_Rating`, `Dielectric`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Model_3D`, `Part_Reference`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number`)
VALUES
('CAP-0402-100NF-00001', '100nF', '±10%', '16V', 'X7R', '0402', 'CAP CER 0.1UF 16V X7R 0402', 'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Samsung', 'CL05B104KO5NNNC', 'Active', 'C1525'),
('CAP-0402-10NF-00002',  '10nF',  '±10%', '50V', 'X7R', '0402', 'CAP CER 10NF 50V X7R 0402',   'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Samsung', 'CL05B103KB5NNNC', 'Active', 'C1523'),
('CAP-0402-22PF-00003',  '22pF',  '±5%',  '50V', 'C0G', '0402', 'CAP CER 22PF 50V C0G 0402',   'DISCRETE\\CAP', 'C0402', 'C0402.step', 'C', 'Murata',  'GRM1555C1H220JA01D', 'Active', 'C1554'),
('CAP-0603-10UF-00004',  '10uF',  '±20%', '10V', 'X5R', '0603', 'CAP CER 10UF 10V X5R 0603',   'DISCRETE\\CAP', 'C0603', 'C0603.step', 'C', 'Samsung', 'CL10A106MP8NNNC', 'Active', 'C15850');

-- 恢复外键检查
SET FOREIGN_KEY_CHECKS = 1;

-- ==============================================================================
-- 视图：全库物料全局检索视图 (用于跨表搜索/BOM查重)
-- ==============================================================================
CREATE OR REPLACE VIEW `v_all_components` AS
SELECT 'Resistors' AS `Category`, `Part_Number`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_resistors`
UNION ALL
SELECT 'Capacitors', `Part_Number`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_capacitors`
UNION ALL
SELECT 'Inductors', `Part_Number`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_inductors`
UNION ALL
SELECT 'Diodes', `Part_Number`, `Value`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_diodes`
UNION ALL
SELECT 'Transistors', `Part_Number`, `Type`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_transistors`
UNION ALL
SELECT 'Power_ICs', `Part_Number`, `Vout`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_power_ics`
UNION ALL
SELECT 'MCUs', `Part_Number`, `Core`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_mcus`
UNION ALL
SELECT 'Analog_ICs', `Part_Number`, `Type`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_analog_ics`
UNION ALL
SELECT 'Logic_Memory', `Part_Number`, `Type`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_logic_memory`
UNION ALL
SELECT 'Connectors', `Part_Number`, `Type`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_connectors`
UNION ALL
SELECT 'Crystals', `Part_Number`, `Frequency`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_crystals`
UNION ALL
SELECT 'Miscellaneous', `Part_Number`, `Type`, `Package`, `Description`, `Schematic_Part`, `PCB_Footprint`, `Manufacturer`, `Manufacturer_Part_Number`, `Lifecycle_Status`, `LCSC_Part_Number` FROM `cis_miscellaneous`;
